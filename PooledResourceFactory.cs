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
        private int _initialCapacity, _maxCapacity, _step, _intervalInMinutes, _prevIntervals, _currentCapacity;
        private int [] _lowestResourcesAvailable;
        private BlockingCollection<T> _resources = new BlockingCollection<T>();
        private Timer _timer;
        private object _lock = new object();
        private bool _disposed = false;
        private Func<T> _factory;

        public PooledResourceFactory(Func<T> func, int initialCapacity = 16, int maxCapacity = 64, int step = 8, 
            int intervalInMinutes = 5, int prevIntervals = 3)
        {
            _initialCapacity = initialCapacity;
            _currentCapacity = initialCapacity;
            _maxCapacity = maxCapacity;
            _step = step;
            _intervalInMinutes = intervalInMinutes;
            _prevIntervals = prevIntervals;
            _factory = func;

            for (int i = 0; i < _initialCapacity; i++)
            {
                T resource = _factory();
                _resources.Add(resource);
            }
            _lowestResourcesAvailable = new int[_prevIntervals];
            for (int i = 0; i < _prevIntervals - 1; i++)
            {
                _lowestResourcesAvailable[i] = -1;
            }
            _lowestResourcesAvailable[_prevIntervals - 1] = _initialCapacity;
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
            lock (_lock)
            {
                if (_lowestResourcesAvailable.All(x => x > _step) && _currentCapacity - _initialCapacity > _step)
                {
                    ScaleIn();
                }
                else if (_lowestResourcesAvailable.All(x => (x < 2) && (x >= 0)) && _currentCapacity < _maxCapacity)
                {
                    ScaleOut();
                }

                for (int i = 1; i < _prevIntervals; i++)
                {
                    _lowestResourcesAvailable[i - 1] = _lowestResourcesAvailable[i];
                }
                _lowestResourcesAvailable[_prevIntervals - 1] = _resources.Count;
                return;   
            }
        }

        public T Acquire()
        {
            T? resource;
            // If resource available, return immediately
            if (_resources.TryTake(out resource))
            {
                if (_resources.Count < _lowestResourcesAvailable[_prevIntervals - 1])
                {
                    _lowestResourcesAvailable[_prevIntervals - 1] = _resources.Count;
                }
                return resource;
            }

            // Increase capacity, if not yet reaching max capacity
            // Increase resource capacity by step
            if (Monitor.TryEnter(_lock))
            {
                try
                {
                    ScaleOut();
                }
                finally 
                { 
                    Monitor.Exit(_lock); 
                }
            }
            
            // Wait for next available resource
            resource = _resources.Take();
            _lowestResourcesAvailable[_prevIntervals - 1] = _resources.Count;
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
