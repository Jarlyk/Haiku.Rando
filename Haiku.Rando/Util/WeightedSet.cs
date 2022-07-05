using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haiku.Rando.Util
{
    public sealed class WeightedSet<T> : IEnumerable<T>
    {
        private readonly List<T> _items = new List<T>();
        private readonly List<double> _partialSums = new List<double>();

        public int Count => _items.Count;

        /// <summary>
        /// Add a new weighted entry to the set; 
        /// </summary>
        /// <param name="weight">Relative weight of item; must be > 0</param>
        /// <param name="item">Item</param>
        public void Add(double weight, T item)
        {
            _items.Add(item);
            var priorSum = _partialSums.Count > 0 ? _partialSums[_partialSums.Count-1] : 0;
            _partialSums.Add(priorSum + weight);
        }

        public bool Remove(T item)
        {
            int index = _items.IndexOf(item);
            if (index == -1)
                return false;

            var priorSum = index > 0 ? _partialSums[index - 1] : 0;
            var weight = _partialSums[index] - priorSum;
            for (int i = index; i < _partialSums.Count - 1; i++)
            {
                _partialSums[i] = _partialSums[i + 1] - weight;
            }
            _partialSums.RemoveAt(_partialSums.Count-1);
            _items.RemoveAt(index);

            return true;
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public void Clear()
        {
            _items.Clear();
            _partialSums.Clear();
        }

        /// <summary>
        /// Choose from the weighted set based on t in [0,1].
        /// If t is drawn from a uniform distribution, then the resulting selection will
        /// distribute based on relative weights.
        /// </summary>
        /// <param name="t">Uniform selection parameter in [0,1]</param>
        /// <returns>Item from set</returns>
        public T PickItem(double t)
        {
            if (_partialSums.Count == 0) throw new InvalidOperationException("Cannot pick from empty set");

            var u = t*_partialSums[_partialSums.Count - 1];
            for (int i = 0; i < _partialSums.Count; i++)
            {
                if (u <= _partialSums[i]) return _items[i];
            }

            //In case of rounding error, return last item
            return _items[_items.Count - 1];
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public static WeightedSet<T> Build(IEnumerable<T> items, Func<T, double> getWeight)
        {
            var set = new WeightedSet<T>();
            foreach (var item in items)
            {
                var weight = getWeight(item);
                if (weight > 0)
                    set.Add(weight, item);
            }

            return set;
        }
    }
}
