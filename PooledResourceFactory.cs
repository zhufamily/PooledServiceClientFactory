using System.Collections.Concurrent;

namespace Ning.Sample
{
    public interface IPooledResourceFactory<T>
    { 
        public T Acquire();
        public void Release(T resource);
        public int TotalCapacity();
        public int AvailableCapacity();
    }

    public class PooledResourceFactory<T> : IPooledResourceFactory<T>, IDisposable
    {
        private int _initialCapacity, _maxCapacity, _step, _intervalInMinutes, _currentCapacity, _lowestResourcesAvailable, _minResourcesRequired;
        private BlockingCollection<T> _resources = new BlockingCollection<T>();
        private Timer _timer;
        private object _lock = new object();
        private bool _disposed = false;
        private Func<T> _factory;

        public PooledResourceFactory(Func<T> func, int initialCapacity = 16, int maxCapacity = 64, int step = 8, 
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

            for (int i = 0; i < _initialCapacity; i++)
            {
                T resource = _factory();
                _resources.Add(resource);
            }
            _timer = new Timer(ScaleResource, null, TimeSpan.FromMinutes(_intervalInMinutes), TimeSpan.FromMinutes(_intervalInMinutes));
        }

        private void ScaleOut()
        {
            for (int i = 0; i < _step; i++)
            {
                if (_currentCapacity >= _maxCapacity)
                {
                    break;
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

        public T Acquire()
        {
            T? resource;
            if (_resources.TryTake(out resource))
            {
                _lowestResourcesAvailable--;
                return resource;
            }

            if (Monitor.TryEnter(_lock))
            {
                try
                {
                    if (_currentCapacity < _maxCapacity)
                    {
                        ScaleOut();
                    }
                }
                finally 
                { 
                    Monitor.Exit(_lock); 
                }
            }

            resource = _resources.Take();
            _lowestResourcesAvailable--;
            return resource;
        }

        public void Release(T resource)
        {
            _resources.Add(resource);
        }

        public int TotalCapacity()
        {
            return _currentCapacity;
        }

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
                    _timer.Dispose();
                }
            }
        }
        #endregion
    }
}
