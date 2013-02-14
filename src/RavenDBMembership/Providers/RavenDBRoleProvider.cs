using System;
using System.Linq;
using System.Web.Security;
using System.Collections.Specialized;
using Microsoft.Practices.ServiceLocation;
using Raven.Client;
using Raven.Client.Linq;
using RavenDBMembership.Entities;

namespace RavenDBMembership.Providers
{
    public class RavenDBRoleProvider : RoleProvider
    {
        private string _providerName = "RavenDBRole";
        private IDocumentStore _documentStore;

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

        public override void Initialize(string name, NameValueCollection config)
        {
            if(config.Keys.Cast<string>().Contains("applicationName"))
                this.ApplicationName = config["applicationName"];

            // Try to find an IDocumentStore via Common Service Locator. 
            try
            {
                var locator = ServiceLocator.Current;
                if(locator != null)
                    this.DocumentStore = locator.GetInstance<IDocumentStore>();
            }
            catch(NullReferenceException) // Swallow Nullreference expection that occurs when there is no current service locator.
            {
            }

            this._providerName = name;
            base.Initialize(name, config);
        }

        public override string ApplicationName { get; set; }

        public override void AddUsersToRoles(string[] usernames, string[] roleNames)
        {
            if(usernames.Length == 0 || roleNames.Length == 0)
                return;

            using(var session = this.DocumentStore.OpenSession())
            {
                try
                {
                    var users = session.Query<User>()
                        .Where(u => u.ApplicationName == this.ApplicationName && u.Username.In(usernames))
                        .ToArray();

                    var roleIds = session.Query<Role>()
                        .Where(u => u.ApplicationName == this.ApplicationName && u.Name.In(roleNames))
                        .Select(r => r.Id)
                        .ToArray();

                    foreach(var user in users)
                        user.Roles = user.Roles.Union(roleIds).ToList();

                    session.SaveChanges();
                }
                catch(Exception ex)
                {
                    this.LogException(ex);
                    throw;
                }
            }
        }

        public override void CreateRole(string roleName)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                try
                {
                    var role = new Role(roleName, null);
                    role.ApplicationName = this.ApplicationName;

                    session.Store(role);
                    session.SaveChanges();
                }
                catch(Exception ex)
                {
                    this.LogException(ex);
                    throw;
                }
            }
        }

        public override bool DeleteRole(string roleName, bool throwOnPopulatedRole)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                try
                {
                    var role = session.Query<Role>()
                        .SingleOrDefault(r => r.ApplicationName == this.ApplicationName && r.Name == roleName);
                    if(role == null)
                        return false;

                    // also find users that have this role
                    var users = session.Query<User>()
                        .Where(u => u.ApplicationName == this.ApplicationName && u.Roles.Any(roleId => roleId == role.Id))
                        .ToList();

                    if(throwOnPopulatedRole && users.Any())
                        throw new Exception(String.Format("Role {0} contains members and cannot be deleted.", role.Name));

                    foreach(var user in users)
                        user.Roles.Remove(role.Id);
                    session.Delete(role);

                    session.SaveChanges();
                    return true;
                }
                catch(Exception ex)
                {
                    this.LogException(ex);
                    throw;
                }
            }
        }

        public override string[] FindUsersInRole(string roleName, string usernameToMatch)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                var role = session.Query<Role>()
                    .SingleOrDefault(r => r.ApplicationName == this.ApplicationName && r.Name == roleName);
                if(role == null)
                    return null;

                return session.Query<User>()
                    .Where(u => u.Roles.Any(r => r == role.Id) && u.Username.Contains(usernameToMatch))
                    .Select(u => u.Username)
                    .ToArray();
            }
        }

        public override string[] GetAllRoles()
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                return session.Query<Role>()
                    .Where(r => r.ApplicationName == this.ApplicationName)
                    .Select(r => r.Name)
                    .ToArray();
            }
        }

        public override string[] GetRolesForUser(string username)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                var userRoleIds = session.Query<User>()
                    .Where(u => u.ApplicationName == this.ApplicationName && u.Username == username)
                    .Select(u => u.Roles)
                    .Single();

                if(!userRoleIds.Any())
                    return new string[0];

                return session.Query<Role>()
                    .Where(r => r.ApplicationName == this.ApplicationName && r.Id.In(userRoleIds))
                    .Select(r => r.Name)
                    .ToArray();
            }
        }

        public override string[] GetUsersInRole(string roleName)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                var role = session.Query<Role>()
                    .SingleOrDefault(r => r.ApplicationName == this.ApplicationName && r.Name == roleName);
                if(role == null)
                    return null;

                return session.Query<User>()
                    .Where(u => u.ApplicationName == this.ApplicationName && u.Roles.Any(r => r == role.Id))
                    .Select(u => u.Username)
                    .ToArray();
            }
        }

        public override bool IsUserInRole(string username, string roleName)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                var user = session.Query<User>()
                    .SingleOrDefault(u => u.ApplicationName == this.ApplicationName && u.Username == username);

                var role = session.Query<Role>()
                    .SingleOrDefault(r => r.ApplicationName == this.ApplicationName && r.Name == roleName);

                return user != null && role != null && user.Roles.Contains(role.Id);
            }
        }

        public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
        {
            if(usernames.Length == 0 || roleNames.Length == 0)
                return;

            using(var session = this.DocumentStore.OpenSession())
            {
                try
                {
                    var users = session.Query<User>()
                        .Where(u => u.ApplicationName == this.ApplicationName && u.Username.In(usernames))
                        .ToArray();

                    var roleIds = session.Query<Role>()
                        .Where(u => u.ApplicationName == this.ApplicationName && u.Name.In(roleNames))
                        .Select(r => r.Id)
                        .ToArray();

                    foreach(var user in users)
                        user.Roles = user.Roles.Except(roleIds).ToList();

                    session.SaveChanges();
                }
                catch(Exception ex)
                {
                    this.LogException(ex);
                    throw;
                }
            }
        }

        public override bool RoleExists(string roleName)
        {
            using(var session = this.DocumentStore.OpenSession())
            {
                return session.Query<Role>().Any(r => r.ApplicationName == this.ApplicationName && r.Name == roleName);
            }
        }

        private void LogException(Exception ex)
        {
            // TODO: log exception properly
            Console.WriteLine(ex.ToString());
        }
    }
}
