using RChecks = Haiku.Rando.Checks;
using RTopology = Haiku.Rando.Topology;
using UE = UnityEngine;

namespace Haiku.Rando.Multiworld
{
    internal class RemoteItem : RTopology.IRandoItem
    {
        public int PlayerId;
        public string Name;
        public RemoteItemState State;

        public int Index;

        public void Give(UE.MonoBehaviour self)
        {
            MWConnection.SendItem(this);
        }

        public bool Obtained() => State != RemoteItemState.Uncollected;

        public RChecks.UIDef UIDef() => new()
        {
            Sprite = null,
            Name = ModText._MW_ITEM_TITLE(Index),
            Description = ModText._MW_ITEM_DESCRIPTION
        };

        public string UIName() => ModText._MW_ITEM_TITLE(Index);

        string RTopology.IRandoItem.Name => $"Multiworld[{Index}]";

        int RTopology.IRandoItem.Index => int.MinValue;
    }

    internal enum RemoteItemState
    {
        Uncollected,
        Collected,
        Confirmed
    }
}