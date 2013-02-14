using System;
using System.Collections.Generic;

namespace RavenDBMembership.Entities
{
	public class User
	{
        private const string DefaultNameSpace = "authorization/users/";
        
        public string Id { get; set; }
		public string ApplicationName { get; set; }
		public string Username { get; set; }
		public string PasswordHash { get; set; }
		public string PasswordSalt { get; set; }
		public string FullName { get; set; }
		public string Email { get; set; }
		public DateTime DateCreated { get; set; }
		public DateTime? DateLastLogin { get; set; }
		public IList<string> Roles { get; set; }

		public User()
		{
			this.Roles = new List<string>();
            this.Id = DefaultNameSpace; // db will append id
		}
	}
}
