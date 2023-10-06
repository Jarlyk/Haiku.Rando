using Haiku.Rando.Logic;
using Haiku.Rando.Checks;
using UnityEngine;

namespace Haiku.Rando.Topology
{
    public interface IRandoItem
    {
        public void Trigger(MonoBehaviour self);

        public bool Obtained();

        public UIDef UIDef();

        public string UIName() => UIDef().Name;

        public string Name { get; }

        public int Index { get; }
    }
}