using System;
using System.Collections.Generic;

namespace CarouselView.FormsPlugin.iOS
{
    public class IndexedCache<TCache> where TCache : class
    {
        private List<CacheItemHolder<TCache>> _cacheHolders;

        public IndexedCache(int count)
        {
            Reset(count);
        }

        public void InsertHolder(int index)
        {
            _cacheHolders.Insert(index, new CacheItemHolder<TCache>());
        }

        public void AddOrReplace(int index, TCache item)
        {
            var holder = _cacheHolders[index];
            if (holder.Item is IDisposable disposable)
            {
                disposable.Dispose();
            }

            holder.Item = item;
        }

        public void Invalidate(int index)
        {
            _cacheHolders[index].Item = null;
        }

        public void Remove(int index)
        {
            var holder = _cacheHolders[index];
            if (holder.Item is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _cacheHolders.RemoveAt(index);
        }

        public void Move(int fromIndex, int toIndex)
        {
            var cacheHolder = _cacheHolders[fromIndex];
            _cacheHolders.RemoveAt(fromIndex);
            _cacheHolders.Insert(toIndex, cacheHolder);
        }

        public void Reset(int count)
        {
            _cacheHolders?.Clear();

            _cacheHolders = new List<CacheItemHolder<TCache>>(count);
            for (int index = 0; index < count; index++)
            {
                _cacheHolders.Add(new CacheItemHolder<TCache>());
            }
        }

        public void Clear()
        {
            foreach (var holder in _cacheHolders)
            {
                if (holder.Item is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                holder.Item = null;
            }

            _cacheHolders.Clear();
        }

        public bool TryGetItem(int index, out TCache item)
        {
            item = null;
            if (index >= _cacheHolders.Count)
            {
                return false;
            }

            if (_cacheHolders[index].HasItem)
            {
                item = _cacheHolders[index].Item;
            }

            return item != null;
        }

        public IList<TCache> ToItemList()
        {
            var result = new List<TCache>();
            foreach (var holder in _cacheHolders)
            {
                if (holder.HasItem)
                {
                    result.Add(holder.Item);
                }
            }

            return result;
        }

        private class CacheItemHolder<TItem>
        {
            public bool HasItem => Item != null;

            public TItem Item { get; set; }
        }
    }
}