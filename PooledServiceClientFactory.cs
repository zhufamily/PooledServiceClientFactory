using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Ning.Sample
{
    /// <summary>
    /// Public interface for Service Client pool
    /// </summary>
    public interface IPooledServiceClientFactory : IPooledResourceFactory<ServiceClient>
    {
        public void Initialize();
    }

    /// <summary>
    /// Main class for Service Client pool
    /// </summary>
    public class PooledServiceClientFactory : PooledResourceFactory<ServiceClient>, IPooledServiceClientFactory
    {
        #region Private members
        private ServiceClient? _masterServiceClient = null;
        private readonly string _dataverseConnectionString;
        private bool _disposed = false;
        #endregion

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="dataverseConnectionString"></param>
        public PooledServiceClientFactory(string dataverseConnectionString) : base(null, true)
        {
            _dataverseConnectionString = dataverseConnectionString;
        }

        /// <summary>
        /// Initialize the pool for Service Client
        /// </summary>
        public void Initialize()
        {
            _masterServiceClient = new ServiceClient(_dataverseConnectionString);
            base.Initialize(() =>
            {
                ServiceClient serviceClient = _masterServiceClient.Clone();
                serviceClient.UseWebApi = true;
                serviceClient.DisableCrossThreadSafeties = true;
                return serviceClient;
            });
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
                    if (_masterServiceClient != null)
                    {
                        _masterServiceClient.Dispose();
                    }
                }
            }

            base.Dispose(disposing);
        }
        #endregion
    }
}
