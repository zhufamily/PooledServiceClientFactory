using Microsoft.PowerPlatform.Dataverse.Client;
using System.Collections.Concurrent;

namespace Ning.Sample
{
    /// <summary>
    /// Public interface for Service Client pool
    /// </summary>
    public interface IPooledServiceClientFactory
    {
        /// <summary>
        /// Get / loan a resource from the pool
        /// </summary>
        /// <returns></returns>
        public ServiceClient Acquire();
        /// <summary>
        /// Return loaded resource to the pool
        /// </summary>
        /// <param name="serviceClient"></param>
        public void Release(ServiceClient serviceClient);
        /// <summary>
        /// Current pool capacity
        /// </summary>
        /// <returns></returns>
        public int TotalCapacity();
        /// <summary>
        /// Current available resources in the pool
        /// </summary>
        /// <returns></returns>
        public int AvailableCapacity();
    }

    /// <summary>
    /// Main class for Service Client pool
    /// </summary>
    public class PooledServiceClientFactory : IPooledServiceClientFactory, IDisposable
    {
        #region Private Members
        private string _dataverseConnectionString;
        private BlockingCollection<ServiceClient> _serviceClientPool;
        private bool _disposed = false;
        private object _lock = new object();
        private int _initialCapacity;
        private int _currentCapacity;
        private int _maxCapacity;
        private int _step;
        private int _lowestAvailableCapacity;
        private readonly Timer _timer;
        #endregion

        /// <summary>
        /// Public constructor
        /// </summary>
        public PooledServiceClientFactory(string dataverseConnectionString, int capacity = 16)
        {
            // Init private members
            _dataverseConnectionString = dataverseConnectionString;
            _serviceClientPool = new BlockingCollection<ServiceClient>();
            _initialCapacity = capacity;
            _currentCapacity = capacity;
            _maxCapacity = capacity * 4;
            _step = _currentCapacity / 2 > 1 ? _currentCapacity / 2 : 1;
            _lowestAvailableCapacity = _currentCapacity;
            for (int i = 0; i < _currentCapacity; i++)
            {
                ServiceClient client = new ServiceClient(_dataverseConnectionString);
                client.UseWebApi = true;
                client.DisableCrossThreadSafeties = true;
                _serviceClientPool.Add(client);
            }
            _timer = new Timer(Scaleback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        /// <summary>
        /// Scaleback -- check every 30 minutes
        /// </summary>
        /// <param name="state"></param>
        private void Scaleback(object? state = null)
        {
            lock (_lock)
            {
                if (_currentCapacity - _initialCapacity >= _step && _lowestAvailableCapacity >= _step)
                {
                    for (int i = 0; i < _step; i++)
                    {
                        if (_serviceClientPool.TryTake(out ServiceClient? discardClient))
                        {
                            discardClient.Dispose();
                            _currentCapacity--;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                _lowestAvailableCapacity = _serviceClientPool.Count;
            }
        }

        /// <summary>
        /// Acquire a service client available
        /// Only for scoped variable
        /// Never used as shared or static variable
        /// </summary>
        /// <returns></returns>
        public ServiceClient Acquire()
        {
            // If resource available, return immediately
            if (_serviceClientPool.TryTake(out ServiceClient? resource))
            {
                if (_serviceClientPool.Count < _lowestAvailableCapacity)
                {
                    _lowestAvailableCapacity = _serviceClientPool.Count;
                }
                return resource;
            }

            // Increase capacity, if not yet reaching max capacity
            // Increase resource capacity by step
            lock (_lock)
            {
                int count = 0;
                while (count < _step && _currentCapacity < _maxCapacity)
                {
                    ServiceClient client = new ServiceClient(_dataverseConnectionString);
                    client.UseWebApi = true;
                    client.DisableCrossThreadSafeties = true;
                    _serviceClientPool.Add(client);
                    _currentCapacity++;
                    count++;
                }
            }

            // Wait for next available resource
            ServiceClient scopedClient = _serviceClientPool.Take();
            if (_serviceClientPool.Count < _lowestAvailableCapacity)
            {
                _lowestAvailableCapacity = _serviceClientPool.Count;
            }
            return scopedClient;
        }

        /// <summary>
        /// Put a service client back to pool
        /// Only if loaded out a resource from the pool
        /// Never put a newly created resource into the pool
        /// </summary>
        /// <param name="serviceClient"></param>
        public void Release(ServiceClient serviceClient)
        {
            _serviceClientPool.Add(serviceClient);
        }

        /// <summary>
        /// Get current resource count
        /// Not resources available, but total resources in recycling
        /// </summary>
        /// <returns></returns>
        public int TotalCapacity()
        {
            return _currentCapacity;
        }

        /// <summary>
        /// Get current available resource count
        /// Not total resources in recycling but resources available
        /// </summary>
        /// <returns></returns>
        public int AvailableCapacity()
        {
            return _serviceClientPool.Count;
        }

        #region IDispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    // Release all resources in the pool
                    // If loaned out resources are NOT returned to the pool
                    // Assuming those are released elsewhere
                    foreach (ServiceClient client in _serviceClientPool)
                    {
                        client.Dispose();
                    }
                }
            }
        }

        #endregion
    }
}
