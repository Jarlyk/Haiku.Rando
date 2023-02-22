namespace Haiku.Rando
{
    public static class Text
    {
        public static string _LORE_TITLE => "_LORE_TITLE";
        public static string _LORE_DESCRIPTION => "_LORE_DESCRIPTION";

        private static void Load(On.LocalizationSystem.orig_Init orig)
        {
            orig();
            LocalizationSystem.localizedEN[_LORE_TITLE] = "Lore";
            LocalizationSystem.localizedEN[_LORE_DESCRIPTION] = "The digitised wisdom of past Arcadians.";
        }

        internal static void Hook()
        {
            On.LocalizationSystem.Init += Load;
        }
    }
}