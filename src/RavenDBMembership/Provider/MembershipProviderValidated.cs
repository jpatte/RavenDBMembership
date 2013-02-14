using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Security;
using RavenDBMembership.UserStrings;

namespace RavenDBMembership.Provider
{
    public abstract class MembershipProviderValidated : MembershipProvider
    {
        public abstract MembershipUser CreateUserSafe(string username, string password, string email,
            string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status);
        public abstract bool CheckPassword(string username, string password, bool updateLastLogin);
        public abstract bool ChangePasswordSafe(string username, string oldPassword, string newPassword);
        public abstract bool DeleteUserSafe(string username, bool deleteAllRelatedData);

        private MembershipCreateStatus ValidateUserCreationArgs(string username, string password, string email, string passwordQuestion, 
            string passwordAnswer, bool isApproved, object providerUserKey)
        {
            if(!SecUtility.ValidateParameter(ref password, true, true, false, 0x80))
                return MembershipCreateStatus.InvalidPassword;

            if(!SecUtility.ValidateParameter(ref username, true, true, true, 0x100))
                return MembershipCreateStatus.InvalidUserName;

            if(!SecUtility.ValidateParameter(ref email, this.RequiresUniqueEmail, this.RequiresUniqueEmail, false, 0x100))
                return MembershipCreateStatus.InvalidEmail;

            if(password.Length < this.MinRequiredPasswordLength)
                return MembershipCreateStatus.InvalidPassword;

            int numNonAlphanumericCharacters = password.Where((t, i) => !char.IsLetterOrDigit(password, i)).Count();
            if(numNonAlphanumericCharacters < this.MinRequiredNonAlphanumericCharacters)
                return MembershipCreateStatus.InvalidPassword;

            if(this.PasswordStrengthRegularExpression.Length > 0 && !Regex.IsMatch(password, this.PasswordStrengthRegularExpression))
                return MembershipCreateStatus.InvalidPassword;

            var e = new ValidatePasswordEventArgs(username, password, true);
            this.OnValidatingPassword(e);
            if(e.Cancel)
                return MembershipCreateStatus.InvalidPassword;

            return MembershipCreateStatus.Success;
        }

        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, 
            string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            status = this.ValidateUserCreationArgs(username, password, email, passwordQuestion, passwordAnswer, isApproved, providerUserKey);
            if(status != MembershipCreateStatus.Success)
                return null;

            return this.CreateUserSafe(username, password, email, passwordQuestion, passwordAnswer, isApproved, providerUserKey, out status);
        }

        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            SecUtility.CheckParameter(ref username, true, true, true, 0x100, "username");
            SecUtility.CheckParameter(ref oldPassword, true, true, false, 0x80, "oldPassword");
            SecUtility.CheckParameter(ref newPassword, true, true, false, 0x80, "newPassword");

            if(!this.CheckPassword(username, oldPassword, false))
                return false;

            if(newPassword.Length < this.MinRequiredPasswordLength)
                throw new ArgumentException("Password is shorter than the minimum " + this.MinRequiredPasswordLength, "newPassword");

            int numNonAlphanumericCharacters = newPassword.Where((t, i) => !char.IsLetterOrDigit(newPassword, i)).Count();
            if(numNonAlphanumericCharacters < this.MinRequiredNonAlphanumericCharacters)
            {
                throw new ArgumentException(
                    SR.Password_need_more_non_alpha_numeric_chars_1.WithParameters(MinRequiredNonAlphanumericCharacters), "newPassword");
            }

            if(this.PasswordStrengthRegularExpression.Length > 0 && !Regex.IsMatch(newPassword, this.PasswordStrengthRegularExpression))
                throw new ArgumentException(SR.Password_does_not_match_regular_expression.WithParameters(), "newPassword");

            var e = new ValidatePasswordEventArgs(username, newPassword, false);
            this.OnValidatingPassword(e);
            if(e.Cancel)
            {
                if(e.FailureInformation != null)
                    throw e.FailureInformation;
                throw new ArgumentException(SR.Membership_Custom_Password_Validation_Failure.WithParameters(), "newPassword");
            }

            return this.ChangePasswordSafe(username, oldPassword, newPassword);
        }

        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            SecUtility.CheckParameter(ref username, true, true, true, 0x100, "username");

            return this.DeleteUserSafe(username, deleteAllRelatedData);
        }
    }
}
