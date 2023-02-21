using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haiku.Rando
{
    /// <summary>
    /// This helper component allows individual objects to create a consistent series of rng results for a given master seed.
    /// The intention of this is to help keep random behavior on a per-object level synchronized for racing purposes.
    /// </summary>
    public sealed class SyncedRng : MonoBehaviour
    {
        public static ulong SequenceSeed;

        private Xoroshiro128Plus _random;

        public Xoroshiro128Plus Random => _random;

        private void Configure()
        {
            var objName = gameObject.name;
            // GetHashCode shouldn't be used here, docs say it's not guaranteed to be consistent between processes
            var seed = !string.IsNullOrEmpty(objName) ? objName.GetHashCode() : 1234;
            seed ^= SceneManager.GetActiveScene().buildIndex;
            _random = new Xoroshiro128Plus(SequenceSeed ^ (ulong)((long)seed - int.MinValue));
        }

        public static SyncedRng Get(GameObject owner)
        {
            var rng = owner.GetComponent<SyncedRng>();
            if (!rng)
            {
                rng = owner.AddComponent<SyncedRng>();
                rng.Configure();
            }

            return rng;
        }
    }
}
