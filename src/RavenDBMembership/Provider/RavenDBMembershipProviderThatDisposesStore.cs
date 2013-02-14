using System;

namespace RavenDBMembership.Provider
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