using System.Collections.Concurrent;

namespace WinAvfs.Core
{
    public class ConcurrentObjectPool<T>(Func<T> factory)
    {
        private readonly ConcurrentBag<T> _pool = new();

        public T Get()
        {
            return _pool.TryTake(out var item) ? item : factory();
        }

        public void Put(T item)
        {
            _pool.Add(item);
        }

        public T[] GetAll()
        {
            return _pool.ToArray();
        }
    }
}