using System;
using System.Collections.Generic;
using System.Text;

namespace RavenDBMembership.Entities
{
	public class User
	{
        private const string DefaultNameSpace = "membership/users/";

        private string _id;
	    private string _applicationName;
	    private string _username;

	    public string Id
        {
            get { return !string.IsNullOrEmpty(this._id) ? this._id : (this._id = GenerateId(this.ApplicationName, this.Username)); }
	        set { _id = value; }
        }

        public string ApplicationName
        {
            get { return _applicationName; }
            set
            {
                _applicationName = value;
                _id = null;
            }
        }

	    public string Username
	    {
	        get { return _username; }
            set 
            { 
                _username = value;
                _id = null;
            }
	    }

	    public string PasswordHash { get; set; }
		public string PasswordSalt { get; set; }
		public string FullName { get; set; }
		public string Email { get; set; }
		public DateTime DateCreated { get; set; }
		public DateTime? DateLastLogin { get; set; }
        public IList<string> Roles { get; set; }
        public bool IsApproved { get; set; }

		public User()
		{
			this.Roles = new List<string>();
		    this.IsApproved = true;
		}

        public static string GenerateId(string applicationName, string username)
        {
            var idBuilder = new StringBuilder();
            idBuilder.Append(DefaultNameSpace);

            if(!String.IsNullOrEmpty(applicationName))
                idBuilder.Append(applicationName.Replace("-", String.Empty)).Append("/");

            return idBuilder.Append(username).ToString();
        }
    }
}
