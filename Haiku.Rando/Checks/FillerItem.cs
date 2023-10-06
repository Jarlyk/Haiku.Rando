using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class FillerItem : IRandoItem
    {
        private int _saveIndex;

        public FillerItem(int i)
        {
            _saveIndex = i;
        }

        public void Give(MonoBehaviour self)
        {
            RandoPlugin.CurrentSaveData.CollectedFillers.Add(_saveIndex);
        }

        public bool Obtained() => RandoPlugin.CurrentSaveData.CollectedFillers.Contains(_saveIndex);

        public UIDef UIDef() => new()
        {
            Sprite = null,
            Name = ModText._NOTHING_TITLE,
            Description = ModText._NOTHING_DESCRIPTION
        };

        public string UIName() => ModText._NOTHING_TITLE;

        public string Name => $"Filler[{_saveIndex}]";

        public int Index { get; set; }
    }
}