using Haiku.Rando.Checks;

namespace Haiku.Rando
{
    internal class SaveData
    {
        private const string presenceKey = "hasRandoData";
        private const string seedKey = "randoSeed";
        private const string collectedLoreKey = "randoCollectedLoreTablets";

        public ulong Seed;
        public ulong CollectedLore;

        public static SaveData Load(ES3File saveFile) =>
            saveFile.Load<bool>(presenceKey, false) ? new(saveFile) : null;

        public SaveData(ulong seed) {
            Seed = seed;
        }

        private SaveData(ES3File saveFile)
        {
            Seed = saveFile.Load<ulong>(seedKey, 0UL);
            CollectedLore = saveFile.Load<ulong>("randoCollectedLoreTablets", 0UL);
        }

        public void SaveTo(ES3File saveFile)
        {
            saveFile.Save(presenceKey, true);
            saveFile.Save(seedKey, Seed);
            saveFile.Save(collectedLoreKey, CollectedLore);
            saveFile.Sync();
        }

        private static ulong LoreMask(int i)
        {
            if (!(i >= 0 && i < 64))
            {
                throw new System.IndexOutOfRangeException($"index {i} for lore is out of range [0,64[");
            }
            return 1UL << i;
        }

        public bool IsLoreCollected(int i) => (CollectedLore & LoreMask(i)) != 0;
        public void MarkLoreCollected(int i)
        {
            CollectedLore |= LoreMask(i);
        }
    }
}