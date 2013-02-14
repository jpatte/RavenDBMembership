using System;
using System.Text;

namespace RavenDBMembership.Entities
{
    public class UniqueFieldConstraint
    {
        private const string DefaultNameSpace = "membership/constraints/";

        private string _id;
        private string _applicationName;
        private string _fieldName;
        private string _value;

        public string Id
        {
            get { return !string.IsNullOrEmpty(this._id) ? this._id : (this._id = GenerateId(this.ApplicationName, this.FieldName, this.Value)); }
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

        public string FieldName
        {
            get { return _fieldName; }
            set
            {
                _fieldName = value;
                _id = null;
            }
        }

        public string Value
        {
            get { return _value; }
            set
            {
                _value = value;
                _id = null;
            }
        }

        public UniqueFieldConstraint(string applicationName, string fieldName, string value)
        {
            this.ApplicationName = applicationName;
            this.FieldName = fieldName;
            this.Value = value;
        }

        public static string GenerateId(string applicationName, string fieldName, string value)
        {
            var idBuilder = new StringBuilder();
            idBuilder.Append(DefaultNameSpace).Append(fieldName).Append("/");

            if(!String.IsNullOrEmpty(applicationName))
                idBuilder.Append(applicationName.Replace("-", String.Empty)).Append("/");

            return idBuilder.Append(value).ToString();
        }
   }
}
