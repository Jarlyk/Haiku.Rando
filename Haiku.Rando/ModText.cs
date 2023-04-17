namespace Haiku.Rando
{
    public static class ModText
    {
        public static string _LORE_TITLE => "_LORE_TITLE";
        public static string _LORE_DESCRIPTION => "_LORE_DESCRIPTION";
        public static string _NOTHING_TITLE => "_NOTHING_TITLE";
        public static string _NOTHING_DESCRIPTION => "_NOTHING_DESCRIPTION";

        private static void Load(On.LocalizationSystem.orig_Init orig)
        {
            orig();
            LocalizationSystem.localizedEN[_LORE_TITLE] = "Lore";
            LocalizationSystem.localizedEN[_LORE_DESCRIPTION] = "The digitised wisdom of past Arcadians.";
            LocalizationSystem.localizedEN[_NOTHING_TITLE] = "Nothing";
            LocalizationSystem.localizedEN[_NOTHING_DESCRIPTION] = "This page intentionally left blank.";
        }

        internal static void Hook()
        {
            On.LocalizationSystem.Init += Load;
        }
    }
}