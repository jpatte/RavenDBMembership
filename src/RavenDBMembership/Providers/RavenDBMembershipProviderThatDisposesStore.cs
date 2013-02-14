using System;

namespace RavenDBMembership.Providers
{
    public class RavenDBMembershipProviderThatDisposesStore : RavenDBMembershipProvider, IDisposable
    {
        public void Dispose()
        {
            if(this.DocumentStore != null)
                this.DocumentStore.Dispose();

            this.DocumentStore = null;
        }
    }
}