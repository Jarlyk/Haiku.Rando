using Haiku.Rando.Logic;
using Haiku.Rando.Checks;
using UnityEngine;

namespace Haiku.Rando.Topology
{
    public interface IRandoItem
    {
        public void Give(MonoBehaviour self);

        public bool Obtained();

        public UIDef UIDef();

        public string UIName();

        public int Index { get; }
    }

    public static class RandoItemExtensions
    {
        public static void Trigger(this IRandoItem item, MonoBehaviour self)
        {
            item.Give(self);
            CheckManager.ShowCheckPopup(item, LocationText.OfCurrentScene());
        }
    }
}