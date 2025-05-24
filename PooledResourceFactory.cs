using System.Collections.Concurrent;

namespace Ning.Sample
{
    /// <summary>
    /// Public interface for resource pool management
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IPooledResourceFactory<T>
    {
        public T Acquire();
        public void Release(T resource);
        public int TotalCapacity();
        public int AvailableCapacity();
    }

    /// <summary>
    /// Generic class for resource pool management
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PooledResourceFactory<T> : IPooledResourceFactory<T>, IDisposable
    {
        #region Private members
        private int _initialCapacity, _maxCapacity, _step, _intervalInMinutes, _currentCapacity, _lowestResourcesAvailable, _minResourcesRequired;
        private BlockingCollection<T> _resources = new BlockingCollection<T>();
        private Timer? _timer = null;
        private object _lock = new object();
        private bool _disposed = false;
        private bool _initialized = false;
        private Func<T>? _factory = null;
        #endregion

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="func"></param>
        /// <param name="initialCapacity"></param>
        /// <param name="maxCapacity"></param>
        /// <param name="step"></param>
        /// <param name="intervalInMinutes"></param>
        /// <param name="minResourcesRequired"></param>
        public PooledResourceFactory(Func<T>? func = null, bool delayInitialization = false, int initialCapacity = 16, int maxCapacity = 64, int step = 8,
            int intervalInMinutes = 5, int minResourcesRequired = 2)
        {
            _initialCapacity = initialCapacity;
            _currentCapacity = initialCapacity;
            _lowestResourcesAvailable = _initialCapacity;
            _maxCapacity = maxCapacity;
            _step = step;
            _intervalInMinutes = intervalInMinutes;
            _minResourcesRequired = minResourcesRequired;
            _factory = func;

            if (!delayInitialization)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Delayed initialization
        /// </summary>
        /// <param name="func"></param>
        public void Initialize(Func<T> func)
        {
            _factory = func;
            Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="NullReferenceException"></exception>
        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            lock (_lock)
            {
                if (_initialized)
                {
                    return;
                }

                if (_factory == null)
                {
                    throw new NullReferenceException("Factory function must be provided before initialization!");
                }
                for (int i = 0; i < _initialCapacity; i++)
                {
                    T resource = _factory();
                    _resources.Add(resource);
                }
                _timer = new Timer(ScaleResource, null, TimeSpan.FromMinutes(_intervalInMinutes), TimeSpan.FromMinutes(_intervalInMinutes));

                _initialized = true;
            }
        }

        #region Auto scaling
        private void ScaleOut()
        {
            for (int i = 0; i < _step; i++)
            {
                if (_currentCapacity >= _maxCapacity)
                {
                    break;
                }

                if (_factory == null)
                {
                    throw new NullReferenceException("Factory function must be provided before auto scaling!");
                }
                T newResource = _factory();
                _resources.Add(newResource);
                _currentCapacity++;
            }
        }

        private void ScaleIn()
        {
            for (int i = 0; i < _step; i++)
            {
                if (_resources.TryTake(out T? discardResource))
                {
                    if (discardResource != null && discardResource is IDisposable)
                    {
                        ((IDisposable)discardResource).Dispose();
                    }
                    _currentCapacity--;
                }
                else
                {
                    break;
                }
            }
        }

        private void ScaleResource(object? state = null)
        {
            if (Monitor.TryEnter(_lock))
            {
                try
                {
                    if (_lowestResourcesAvailable > _step && _currentCapacity - _initialCapacity > _step)
                    {
                        ScaleIn();
                    }
                    else if (_lowestResourcesAvailable < _minResourcesRequired && _currentCapacity < _maxCapacity)
                    {
                        ScaleOut();
                    }

                    _lowestResourcesAvailable = _resources.Count;
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
        }
        #endregion

        /// <summary>
        /// Loan a resource from the pool
        /// </summary>
        /// <returns></returns>
        public T Acquire()
        {
            T? resource;
            if (_resources.TryTake(out resource))
            {
                if (_resources.Count < _lowestResourcesAvailable)
                {
                    _lowestResourcesAvailable = _resources.Count;
                }
                return resource;
            }

            ScaleResource();

            resource = _resources.Take();
            if (_resources.Count < _lowestResourcesAvailable)
            {
                _lowestResourcesAvailable = _resources.Count;
            }
            return resource;
        }

        /// <summary>
        /// Release a object back to the pool
        /// </summary>
        /// <param name="resource"></param>
        public void Release(T resource)
        {
            _resources.Add(resource);
        }

        /// <summary>
        /// Get total capacity of the pool
        /// </summary>
        /// <returns></returns>
        public int TotalCapacity()
        {
            return _currentCapacity;
        }

        /// <summary>
        /// Get available capacity of the pool
        /// </summary>
        /// <returns></returns>
        public int AvailableCapacity()
        {
            return _resources.Count;
        }

        #region IDispose
        ~PooledResourceFactory() => Dispose(false);

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
                    foreach (T resource in _resources)
                    {
                        if (resource != null && resource is IDisposable)
                        {
                            ((IDisposable)resource).Dispose();
                        }
                    }
                    _resources.Dispose();
                    if (_timer != null)
                    {
                        _timer.Dispose();
                    }
                }
            }
        }
        #endregion
    }
}
