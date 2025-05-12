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
        /// <summary>
        /// Public constructor
        /// </summary>
        public PooledServiceClientFactory(string dataverseConnectionString) : base(() => { 
            ServiceClient serviceClient = new ServiceClient(dataverseConnectionString);
            serviceClient.UseWebApi = true;
            serviceClient.DisableCrossThreadSafeties = true;
            return serviceClient;
        })
        {
        }
    }
}
