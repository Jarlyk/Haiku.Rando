using Haiku.Rando.Checks;

namespace Haiku.Rando
{
    internal class SaveData
    {
        private const string presenceKey = "hasRandoData";
        private const string seedKey = "randoSeed";
        private const string collectedLoreKey = "randoCollectedLoreTablets";
        private const string collectedFillerKey = "randoCollectedFillers";

        public ulong Seed;
        public Bitset64 CollectedLore;
        public Bitset64 CollectedFillers;

        public static SaveData Load(ES3File saveFile) =>
            saveFile.Load<bool>(presenceKey, false) ? new(saveFile) : null;

        public SaveData(ulong seed) {
            Seed = seed;
        }

        private SaveData(ES3File saveFile)
        {
            Seed = saveFile.Load<ulong>(seedKey, 0UL);
            CollectedLore = new(saveFile.Load<ulong>(collectedLoreKey, 0UL));
            CollectedFillers = new(saveFile.Load<ulong>(collectedFillerKey, 0UL));
        }

        public void SaveTo(ES3File saveFile)
        {
            saveFile.Save(presenceKey, true);
            saveFile.Save(seedKey, Seed);
            saveFile.Save(collectedLoreKey, CollectedLore.Bits);
            saveFile.Save(collectedFillerKey, CollectedFillers.Bits);
            saveFile.Sync();
        }
    }
}