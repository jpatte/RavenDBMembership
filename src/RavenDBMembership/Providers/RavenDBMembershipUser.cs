using System;
using System.Web.Security;

namespace RavenDBMembership.Providers
{
    class RavenDBMembershipUser : MembershipUser
    {
        public RavenDBMembershipUser(string providerName, string username, string id, string email, string passwordQuestion, string comment, bool isApproved, bool isLockedOut, DateTime creationDate, DateTime lastLoginDate, DateTime lastActivityDate, DateTime lastPasswordChangedDate, DateTime lastLockoutDate)
            : base(providerName, username, id, email, passwordQuestion, comment, isApproved, isLockedOut, creationDate, lastLoginDate, lastActivityDate, lastPasswordChangedDate, lastLockoutDate)
        {
        }

        public override bool ChangePassword(string oldPassword, string newPassword)
        {
            return Membership.Providers[this.ProviderName].ChangePassword(this.UserName, oldPassword, newPassword);
        }
    }
}
