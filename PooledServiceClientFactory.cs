using Microsoft.PowerPlatform.Dataverse.Client;

namespace Ning.Sample
{
    /// <summary>
    /// Public interface for Service Client pool
    /// </summary>
    public interface IPooledServiceClientFactory : IPooledResourceFactory<ServiceClient>
    {
    }

    /// <summary>
    /// Main class for Service Client pool
    /// </summary>
    public class PooledServiceClientFactory : PooledResourceFactory<ServiceClient>, IPooledServiceClientFactory
    {
        #region Private members
        private ServiceClient _masterServiceClient;
        private bool _disposed = false;
        #endregion

        /// <summary>
        /// Factory pattern to get instance
        /// </summary>
        /// <param name="dataverseConnectionString"></param>
        /// <returns></returns>
        public static PooledServiceClientFactory CreatePooledServiceClientFactory(string dataverseConnectionString)
        {
            ServiceClient serviceClient = new ServiceClient(dataverseConnectionString);
            return new PooledServiceClientFactory(serviceClient);
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        private PooledServiceClientFactory(ServiceClient masterServiceClient) : base(() => { 
            ServiceClient serviceClient = masterServiceClient.Clone();
            serviceClient.UseWebApi = true;
            serviceClient.DisableCrossThreadSafeties = true;
            return serviceClient;
        })
        {
            _masterServiceClient = masterServiceClient;
        }

        #region IDisposable
        ~PooledServiceClientFactory() => Dispose(false);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _masterServiceClient.Dispose();
                }
            }

            base.Dispose(disposing);
        }
        #endregion
    }
}
