using System;
using System.Collections.Generic;
using System.Configuration.Provider;
using System.Linq;
using System.Linq.Expressions;
using System.Web.Security;
using Raven.Abstractions.Exceptions;
using Raven.Client;
using Microsoft.Practices.ServiceLocation;
using System.Collections.Specialized;
using Raven.Client.Document;
using RavenDBMembership.Entities;
using RavenDBMembership.Utils;

namespace RavenDBMembership.Providers
{
    public class RavenDBMembershipProvider : MembershipProviderValidated
    {
        private const string UsersCollectionName = "MembershipUsers";
        private const string ConstraintsCollectionName = "MembershipConstraints";
        private const string EmailConstraintName = "email";

        private string _providerName = "RavenDBMembership";
        private IDocumentStore _documentStore;
        private int _minRequiredPasswordLength = 7;

        public IDocumentStore DocumentStore
        {
            get
            {
                if(this._documentStore == null)
                    throw new NullReferenceException("The DocumentStore is not set. Please set the DocumentStore or make sure that the Common Service Locator can find the IDocumentStore and call Initialize on this provider.");

                return this._documentStore;
            }
            set { this._documentStore = value; }
        }

        public override string ApplicationName { get; set; }
        public override bool EnablePasswordReset { get { return true; } }
        public override bool EnablePasswordRetrieval { get { return false; } }
        public override int MaxInvalidPasswordAttempts { get { return 10; } }
        public override int MinRequiredNonAlphanumericCharacters { get { return 0; } }
        public override int MinRequiredPasswordLength { get { return this._minRequiredPasswordLength; } }
        public override int PasswordAttemptWindow { get { return 5; } }
        public override MembershipPasswordFormat PasswordFormat { get { return MembershipPasswordFormat.Hashed; } }
        public override string PasswordStrengthRegularExpression { get { return String.Empty; } }
        public override bool RequiresQuestionAndAnswer { get { return false; } }
        public override bool RequiresUniqueEmail { get { return false; } }

        public override void Initialize(string name, NameValueCollection config)
        {
            if(config.Keys.Cast<string>().Contains("minRequiredPasswordLength"))
                this._minRequiredPasswordLength = int.Parse(config["minRequiredPasswordLength"]);

            if(config.Keys.Cast<string>().Contains("applicationName"))
                this.ApplicationName = config["applicationName"];

            // Try to find an IDocumentStore via Common Service Locator. 
            try
            {
                var locator = ServiceLocator.Current;
                if(locator != null)
                {
                    this.DocumentStore = locator.GetInstance<IDocumentStore>();

                    var existingConvention = this.DocumentStore.Conventions.FindTypeTagName;
                    this.DocumentStore.Conventions.FindTypeTagName = type =>
                        type == typeof(User) ? UsersCollectionName :
                        type == typeof(UniqueFieldConstraint) ? ConstraintsCollectionName :
                        existingConvention(type);
                }
            }
            catch(NullReferenceException) // Swallow Nullreference expection that occurs when there is no current service locator.
            {
            }

            this._providerName = name;

            base.Initialize(name, config);
        }

        public override bool ChangePasswordSafe(string username, string oldPassword, string newPassword)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                var user = this.LoadUser(session, username);
                if(user == null || user.PasswordHash != PasswordUtil.HashPassword(oldPassword, user.PasswordSalt))
                    throw new MembershipPasswordException("Invalid username or old password.");

                user.PasswordSalt = PasswordUtil.CreateRandomSalt();
                user.PasswordHash = PasswordUtil.HashPassword(newPassword, user.PasswordSalt);

                session.SaveChanges();
            }
            return true;
        }

        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            throw new NotImplementedException();
        }

        public override MembershipUser CreateUserSafe(string username, string password, string email, string passwordQuestion, string passwordAnswer,
            bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            if(password.Length < this.MinRequiredPasswordLength)
                throw new MembershipCreateUserException(MembershipCreateStatus.InvalidPassword);

            var args = new ValidatePasswordEventArgs(username, password, true);
            this.OnValidatingPassword(args);
            if(args.Cancel)
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            var passwordSalt = PasswordUtil.CreateRandomSalt();
            var passwordHash = PasswordUtil.HashPassword(password.Trim(), passwordSalt);
            var user = new User
            {
                Username = username,
                PasswordSalt = passwordSalt,
                PasswordHash = passwordHash,
                Email = email,
                ApplicationName = this.ApplicationName,
                DateCreated = DateTime.UtcNow,
                IsApproved = isApproved
            };

            using(var session = this.DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                try
                {
                    session.Store(user);
                    session.Store(new UniqueFieldConstraint(this.ApplicationName, EmailConstraintName, user.Email));

                    session.SaveChanges();

                    status = MembershipCreateStatus.Success;
                    return this.UserToMembershipUser(user, lastPasswordChangedDate: DateTime.UtcNow);
                }
                catch(ConcurrencyException e)
                {
                    status = this.InterpretConcurrencyException(user.Username, user.Email, e);
                }
                catch(Exception ex)
                {
                    this.LogException(ex);
                    status = MembershipCreateStatus.ProviderError;
                }
            }
            return null;
        }

        public override bool DeleteUserSafe(string username, bool deleteAllRelatedData)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                try
                {
                    var user = this.LoadUser(session, username);
                    if(user == null)
                        throw new NullReferenceException("The user could not be deleted.");

                    session.Delete(session.Load<UniqueFieldConstraint>(
                        UniqueFieldConstraint.GenerateId(this.ApplicationName, EmailConstraintName, user.Email)));

                    session.Delete(user);
                    session.SaveChanges();
                    return true;
                }
                catch(Exception ex)
                {
                    this.LogException(ex);
                    return false;
                }
            }
        }

        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            // TODO: support partial search instead of limiting to exact match
            return this.FindUsers(u => u.Email == emailToMatch, pageIndex, pageSize, out totalRecords);
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            // TODO: support partial search instead of limiting to exact match
            return this.FindUsers(u => u.Username == usernameToMatch, pageIndex, pageSize, out totalRecords);
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            return this.FindUsers(null, pageIndex, pageSize, out totalRecords);
        }

        public override int GetNumberOfUsersOnline()
        {
            throw new NotImplementedException();
        }

        public override string GetPassword(string username, string answer)
        {
            throw new NotSupportedException();
        }

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                var user = this.LoadUser(session, username);
                return user != null ? this.UserToMembershipUser(user) : null;
            }
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                var user = session.Load<User>(providerUserKey.ToString());
                return user != null ? this.UserToMembershipUser(user) : null;
            }
        }

        public override string GetUserNameByEmail(string email)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                return session.Query<User>()
                    .Where(u => u.ApplicationName == this.ApplicationName && u.Email == email)
                    .Select(u => u.Username)
                    .SingleOrDefault();
            }
        }

        public override string ResetPassword(string username, string answer)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                try
                {
                    var user = this.LoadUser(session, username);
                    if(user == null)
                        throw new Exception("The user to reset the password for could not be found.");

                    var newPassword = Membership.GeneratePassword(
                        Math.Max(8, this.MinRequiredPasswordLength), Math.Max(2, this.MinRequiredNonAlphanumericCharacters));
                    user.PasswordSalt = PasswordUtil.CreateRandomSalt();
                    user.PasswordHash = PasswordUtil.HashPassword(newPassword, user.PasswordSalt);

                    session.SaveChanges();
                    return newPassword;
                }
                catch(Exception ex)
                {
                    this.LogException(ex);
                    throw;
                }
            }
        }

        public override bool UnlockUser(string userName)
        {
            throw new NotImplementedException();
        }

        public override void UpdateUser(MembershipUser user)
        {
            if(user == null)
                throw new ArgumentNullException("user");

            string username = user.UserName;
            ParameterUtility.CheckParameter(ref username, true, true, true, 0x100, "UserName");

            string email = user.Email;
            ParameterUtility.CheckParameter(ref email, this.RequiresUniqueEmail, this.RequiresUniqueEmail, false, 0x100, "Email");
            user.Email = email;

            using(var session = this.DocumentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                try
                {
                    var dbUser = this.LoadUser(session, username);
                    if(dbUser == null)
                        throw new Exception("The user to update could not be found.");

                    if(dbUser.Email != user.Email)
                    {
                        session.Delete(session.Load<UniqueFieldConstraint>(
                            UniqueFieldConstraint.GenerateId(this.ApplicationName, EmailConstraintName, dbUser.Email)));
                        session.Store(new UniqueFieldConstraint(this.ApplicationName, EmailConstraintName, user.Email));
                    }

                    dbUser.IsApproved = user.IsApproved;
                    dbUser.Email = user.Email;
                    dbUser.DateCreated = user.CreationDate;
                    dbUser.DateLastLogin = user.LastLoginDate;

                    session.SaveChanges();
                }
                catch(ConcurrencyException ex)
                {
                    var status = this.InterpretConcurrencyException(user.UserName, user.Email, ex);
                    if(status == MembershipCreateStatus.DuplicateEmail)
                        throw new ProviderException("The E-mail supplied is invalid.");

                    throw;
                }
            }
        }

        public override bool ValidateUser(string username, string password)
        {
            return this.CheckPassword(username, password, updateLastLogin: true);
        }

        public override bool CheckPassword(string username, string password, bool updateLastLogin)
        {
            username = username.Trim();
            password = password.Trim();

            using(var session = this.DocumentStore.OpenSession())
            {
                var user = this.LoadUser(session, username);
                if(user == null || user.PasswordHash != PasswordUtil.HashPassword(password, user.PasswordSalt) || !user.IsApproved)
                    return false;

                if(updateLastLogin)
                    user.DateLastLogin = DateTime.UtcNow;

                session.SaveChanges();
                return true;
            }
        }

        private MembershipCreateStatus InterpretConcurrencyException(string username, string email, ConcurrencyException e)
        {
            if(e.Message.Contains(User.GenerateId(this.ApplicationName, username)))
                return MembershipCreateStatus.DuplicateUserName;

            if(e.Message.Contains(UniqueFieldConstraint.GenerateId(this.ApplicationName, EmailConstraintName, email)))
                return MembershipCreateStatus.DuplicateEmail;

            return MembershipCreateStatus.ProviderError;
        }

        private User LoadUser(IDocumentSession session,  string userName)
        {
            return session.Load<User>(User.GenerateId(this.ApplicationName, userName));
        }

        private MembershipUserCollection FindUsers(Expression<Func<User, bool>> predicate, int pageIndex, int pageSize, out int totalRecords)
        {
            IEnumerable<User> pagedUsers;
            using(var session = this.DocumentStore.OpenSession())
            {
                RavenQueryStatistics stats;
                var users = session.Query<User>()
                    .Statistics(out stats)
                    .Where(u => u.ApplicationName == this.ApplicationName);
                if(predicate != null)
                    users = users.Where(predicate);

                pagedUsers = users.Skip(pageIndex * pageSize).Take(pageSize).ToArray();
                totalRecords = stats.TotalResults;
            }

            var membershipUsers = new MembershipUserCollection();
            foreach(var user in pagedUsers)
                membershipUsers.Add(this.UserToMembershipUser(user));
            return membershipUsers;
        }

        private MembershipUser UserToMembershipUser(User user, DateTime? lastPasswordChangedDate = null)
        {
            var defaultDate = new DateTime(1900, 1, 1);
            return new RavenDBMembershipUser(
                providerName: this._providerName,
                username: user.Username,
                id: user.Id,
                email: user.Email,
                passwordQuestion: null,
                comment: null,
                isApproved: user.IsApproved,
                isLockedOut: false,
                creationDate: user.DateCreated,
                lastLoginDate: user.DateLastLogin ?? defaultDate,
                lastActivityDate: defaultDate,
                lastPasswordChangedDate: lastPasswordChangedDate ?? defaultDate,
                lastLockoutDate: defaultDate
            );
        }

        private void LogException(Exception ex)
        {
            // TODO: log exception properly
            Console.WriteLine(ex.ToString());
        }
    }
}
