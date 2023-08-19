using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Haiku.Rando.Util
{
    public sealed class WeightedSet<T>
    {
        private readonly List<T> _items;
        private readonly double[] _partialSums;

        public WeightedSet(List<T> items, Func<T, double> getWeight)
        {
            _items = items;
            if (items.Count > 0)
            {
                _partialSums = new double[_items.Count];
                _partialSums[0] = getWeight(_items[0]);
                for (var i = 1; i < _items.Count; i++)
                {
                    _partialSums[i] = _partialSums[i - 1] + getWeight(_items[i]);
                }
            }
        }

        public int Count => _items.Count;

        private int PickItemIndex(double t)
        {
            if (_items.Count == 0)
            {
                throw new InvalidOperationException("tried to draw item from exhausted WeightedSet");
            }
            var i = BinarySearch(_partialSums, _items.Count, t * _partialSums[_items.Count - 1]);
            //In case of rounding error, return last item
            if (i == _items.Count)
            {
                return i - 1;
            }
            return i;
        }

        /// <summary>
        /// Choose from the weighted set based on t in [0,1].
        /// If t is drawn from a uniform distribution, then the resulting selection will
        /// distribute based on relative weights.
        /// </summary>
        /// <param name="t">Uniform selection parameter in [0,1]</param>
        /// <returns>Item from set</returns>
        public T PickItem(double t) => _items[PickItemIndex(t)];

        /// Like PickItem, but removes the selected item from the set
        /// and from the set's underlying item list (the one passed to
        /// its constructor).
        public T RemoveItem(double t)
        {
            if (_items.Count == 1)
            {
                var x = _items[0];
                _items.Clear();
                return x;
            }

            var i = PickItemIndex(t);
            var item = _items[i];
            _items[i] = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            var wRemoved = _partialSums[i];
            if (i > 0)
            {
                wRemoved -= _partialSums[i - 1];
            }
            var wAdded = _partialSums[_items.Count - 1] - _partialSums[_items.Count - 2];
            for (var j = i; j < _items.Count; j++)
            {
                _partialSums[j] -= wRemoved;
                _partialSums[j] += wAdded;
            }
            return item;
        }

        // Returns the index where y would have to be inserted in xs
        // in order to keep xs sorted.
        // If y is less than any element in xs, this is 0.
        // If y is greater than any element in xs, this is xs.Length.
        private static int BinarySearch(double[] xs, int len, double y)
        {
            var i = Array.BinarySearch(xs, 0, len, y);
            return i < 0 ? ~i : i;
        }
    }
}
