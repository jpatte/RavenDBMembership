using System;
using System.Text;

namespace RavenDBMembership.Entities
{
    public class Role
    {
        private const string DefaultNameSpace = "membership/roles/";

        private string _id;
        private string _applicationName;
        private string _name;

        public string Id
        {
            get { return !string.IsNullOrEmpty(this._id) ? this._id : (this._id = GenerateId(this.ApplicationName, this.Name)); }
            set { this._id = value; }
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

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                _id = null;
            }
        }

        public Role(string applicationName, string name)
        {
            this.ApplicationName = applicationName;
            this.Name = name;
        }

        public static string GenerateId(string applicationName, string roleName)
        {
            var idBuilder = new StringBuilder();
            idBuilder.Append(DefaultNameSpace);

            if(!String.IsNullOrEmpty(applicationName))
                idBuilder.Append(applicationName.Replace("-", String.Empty)).Append("/");

            return idBuilder.Append(roleName).ToString();
        }
    }
}
