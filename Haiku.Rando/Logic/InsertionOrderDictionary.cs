using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Haiku.Rando.Logic
{
    public class InsertionOrderDictionary<K, V> : IReadOnlyDictionary<K, V>
    {
        private readonly Dictionary<K, V> _mapping;
        private readonly K[] _keys;

        public InsertionOrderDictionary(List<(K, V)> entries)
        {
            _mapping = new();
            _keys = new K[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var (k, v) = entries[i];
                _mapping[k] = v;
                _keys[i] = k;
            }
        }

        public V this[K key] {
            get
            {
                return _mapping[key];
            }
        }

        public IEnumerable<K> Keys => _keys;
        public IEnumerable<V> Values => _keys.Select(k => _mapping[k]);
        public bool ContainsKey(K key) => _mapping.ContainsKey(key);
        public bool TryGetValue(K key, out V val) => _mapping.TryGetValue(key, out val);
        public int Count => _keys.Length;

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => _keys.Select(k => new KeyValuePair<K, V>(k, _mapping[k])).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}