using Collections = System.Collections.Generic;
using RLogic = Haiku.Rando.Logic;
using RTopology = Haiku.Rando.Topology;
using CType = Haiku.Rando.Topology.CheckType;

namespace Haiku.Rando.Multiworld
{
    internal class MWSaveData
    {
        private const string presenceKey = "hasMWData";
        private const string addrKey = "mwServerAddr";
        private const string pidKey = "mwPlayerId";
        private const string ridKey = "mwRandoId";
        private const string nRemoteItemsKey = "mwNumRemoteItems";
        private const string remoteItemPidPrefix = "mwRemoteItemPlayerId";
        private const string remoteItemNamePrefix = "mwRemoteItemName";
        private const string remoteItemStatePrefix = "mwRemoteItemState";
        private const string nPlayersKey = "mwNumPlayers";
        private const string playerNicknamePrefix = "mwPlayerNickname";
        private const string nPlacementsKey = "mwNumPlacements";
        private const string placementLocPrefix = "mwPlacementLoc";
        private const string placementItemPrefix = "mwPlacementItem";

        public string ServerAddr;
        public int PlayerId;
        public int RandoId;
        public Collections.List<RemoteItem> RemoteItems;
        public string[] RemoteNicknames;
        public Collections.List<Placement> PatchedPlacements;

        public string SelfNickname => RemoteNicknames[PlayerId];

        public static MWSaveData Load(ES3File saveFile) =>
            saveFile.Load<bool>(presenceKey, false) ? new(saveFile) : null;
        
        public MWSaveData() {}

        public MWSaveData(ES3File saveFile)
        {
            ServerAddr = saveFile.Load<string>(addrKey, "");
            PlayerId = saveFile.Load<int>(pidKey, -1);
            RandoId = saveFile.Load<int>(ridKey, -1);

            var numItems = saveFile.Load<int>(nRemoteItemsKey, 0);
            RemoteItems = new(numItems);
            for (var i = 0; i < numItems; i++)
            {
                RemoteItems.Add(new()
                {
                    PlayerId = saveFile.Load<int>(remoteItemPidPrefix + i, -1),
                    Name = saveFile.Load<string>(remoteItemNamePrefix + i, ""),
                    State = (RemoteItemState)saveFile.Load<int>(remoteItemStatePrefix + i, 0)
                });
            }

            var numPlayers = saveFile.Load<int>(nPlayersKey, 0);
            RemoteNicknames = new string[numPlayers];
            for (var i = 0; i < numPlayers; i++)
            {
                RemoteNicknames[i] = saveFile.Load<string>(playerNicknamePrefix + i, "");
            }

            var numPlacements = saveFile.Load<int>(nPlacementsKey, 0);
            PatchedPlacements = new(numPlacements);
            for (var i = 0; i < numPlacements; i++)
            {
                PatchedPlacements.Add(new()
                { 
                    LocationIndex = saveFile.Load<int>(placementLocPrefix + i, int.MaxValue),
                    ItemIndex = saveFile.Load<int>(placementItemPrefix + i, int.MaxValue)
                });
            }
        }

        public void ApplyText()
        {
            for (var i = 0; i < RemoteItems.Count; i++)
            {
                var ri = RemoteItems[i];
                var ownerName = RemoteNicknames[ri.PlayerId];
                var j = ri.Name.LastIndexOf("_(");
                var itemName = j == -1 ? ri.Name : ri.Name.Substring(0, j);
                itemName = itemName.Replace('_', ' ');
                LocalizationSystem.localizedEN[ModText._MW_ITEM_TITLE(i)] = $"{ownerName}'s {itemName}";
            }
        }

        public void ApplyPlacements(RLogic.CheckRandomizer rando)
        {
            var allChecks = rando.Topology.Checks;

            foreach (var pp in PatchedPlacements)
            {
                var loc = allChecks[pp.LocationIndex];
                var item = pp.ItemIndex >= 0 ?
                    allChecks[pp.ItemIndex] :
                    new RTopology.RandoCheck(CType.Multiworld, 0, new(0, 0), -pp.ItemIndex - 1);
                rando.SetCheckMapping(loc, item);
            }
        }

        public void SaveTo(ES3File saveFile)
        {
            saveFile.Save(presenceKey, true);
            saveFile.Save(addrKey, ServerAddr);
            saveFile.Save(pidKey, PlayerId);
            saveFile.Save(ridKey, RandoId);

            saveFile.Save(nRemoteItemsKey, RemoteItems.Count);
            for (var i = 0; i < RemoteItems.Count; i++)
            {
                var ri = RemoteItems[i];
                saveFile.Save(remoteItemPidPrefix + i, ri.PlayerId);
                saveFile.Save(remoteItemNamePrefix + i, ri.Name);
                saveFile.Save(remoteItemStatePrefix + i, (int)ri.State);
            }

            saveFile.Save(nPlayersKey, RemoteNicknames.Length);
            for (var i = 0; i < RemoteNicknames.Length; i++)
            {
                saveFile.Save(playerNicknamePrefix + i, RemoteNicknames[i]);
            }

            saveFile.Save(nPlacementsKey, PatchedPlacements.Count);
            for (var i = 0; i < PatchedPlacements.Count; i++)
            {
                var p = PatchedPlacements[i];
                saveFile.Save(placementLocPrefix + i, p.LocationIndex);
                saveFile.Save(placementItemPrefix + i, p.ItemIndex);
            }
            // Sync not needed since this is never run on its own,
            // only as part of SaveData.SaveTo
        }
    }

    internal class RemoteItem
    {
        public int PlayerId;
        public string Name;
        public RemoteItemState State;
    }

    internal enum RemoteItemState
    {
        Uncollected,
        Collected,
        Confirmed
    }

    internal struct Placement
    {
        public int LocationIndex;
        public int ItemIndex;
    }
}