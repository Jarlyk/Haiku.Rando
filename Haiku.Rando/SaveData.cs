using Haiku.Rando.Checks;
using Haiku.Rando.Multiworld;

namespace Haiku.Rando
{
    internal class SaveData
    {
        private const string presenceKey = "hasRandoData";
        private const string seedKey = "randoSeed";
        private const string randoStartKey = "randoRandomStart";
        private const string trainLoverModeKey = "randoTrainLover";
        private const string poolsKey = "randoPools";
        private const string startingItemsKey = "randoStartingItems";
        private const string skipsKey = "randoSkips";
        private const string levelKey = "randoLevel";
        private const string includeOldArcadiaKey = "randoIncludeOldArcadia";
        private const string includeLostArchivesKey = "randoIncludeLostArchives";
        
        private const string collectedLoreKey = "randoCollectedLoreTablets";
        private const string collectedFillerKey = "randoCollectedFillers";
        private const string collectedSplunkKey = "randoCollectedSplunk";

        public GenerationSettings Settings;
        public Bitset64 CollectedLore;
        public Bitset64 CollectedFillers;
        public bool CollectedSplunk;
        public MWSaveData MW;

        public static SaveData Load(ES3File saveFile) =>
            saveFile.Load<bool>(presenceKey, false) ? new(saveFile) : null;

        public SaveData(GenerationSettings gs) {
            Settings = gs;
        }

        private SaveData(ES3File saveFile)
        {
            Settings = new()
            {
                Seed = saveFile.Load<string>(seedKey, ""),
                RandomStartLocation = saveFile.Load<bool>(randoStartKey, false),
                TrainLoverMode = saveFile.Load<bool>(trainLoverModeKey, false),
                Pools = new(saveFile.Load<ulong>(poolsKey, 0UL)),
                StartingItems = new(saveFile.Load<ulong>(startingItemsKey, 0UL)),
                Skips = new(saveFile.Load<ulong>(skipsKey, 0UL)),
                Level = (RandomizationLevel)saveFile.Load<int>(levelKey, 0),
                IncludeOldArcadia = saveFile.Load<bool>(includeOldArcadiaKey, false),
                IncludeLostArchives = saveFile.Load<bool>(includeLostArchivesKey, false)
            };
            CollectedLore = new(saveFile.Load<ulong>(collectedLoreKey, 0UL));
            CollectedFillers = new(saveFile.Load<ulong>(collectedFillerKey, 0UL));
            CollectedSplunk = saveFile.Load<bool>(collectedSplunkKey, false);
            MW = MWSaveData.Load(saveFile);
        }

        public void SaveTo(ES3File saveFile)
        {
            saveFile.Save(presenceKey, true);
            saveFile.Save(seedKey, Settings.Seed);
            saveFile.Save(randoStartKey, Settings.RandomStartLocation);
            saveFile.Save(trainLoverModeKey, Settings.TrainLoverMode);
            saveFile.Save(poolsKey, Settings.Pools.Bits);
            saveFile.Save(startingItemsKey, Settings.StartingItems.Bits);
            saveFile.Save(skipsKey, Settings.Skips.Bits);
            saveFile.Save(levelKey, (int)Settings.Level);
            saveFile.Save(includeOldArcadiaKey, Settings.IncludeOldArcadia);
            saveFile.Save(includeLostArchivesKey, Settings.IncludeLostArchives);
            saveFile.Save(collectedLoreKey, CollectedLore.Bits);
            saveFile.Save(collectedFillerKey, CollectedFillers.Bits);
            saveFile.Save(collectedSplunkKey, CollectedSplunk);
            if (MW != null)
            {
                MW.SaveTo(saveFile);
            }
            saveFile.Sync();
        }
    }
}