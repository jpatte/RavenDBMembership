﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RavenDBMembership
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
			Roles = new List<string>();
            Id = DefaultNameSpace; // db will append id
		}
	}
}
