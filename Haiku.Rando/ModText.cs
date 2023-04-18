namespace Haiku.Rando
{
    public static class ModText
    {
        public static string _LORE_TITLE => "_LORE_TITLE";
        public static string _LORE_DESCRIPTION => "_LORE_DESCRIPTION";
        public static string _NOTHING_TITLE => "_NOTHING_TITLE";
        public static string _NOTHING_DESCRIPTION => "_NOTHING_DESCRIPTION";
        public static string _HEALTH_MARKER_DESCRIPTION => "_HEALTH_MARKER_DESCRIPTION";
        public static string _BANK_MARKER_DESCRIPTION => "_BANK_MARKER_DESCRIPTION";
        public static string _TRAIN_MARKER_DESCRIPTION => "_TRAIN_MARKER_DESCRIPTION";
        public static string _VENDOR_MARKER_DESCRIPTION => "_VENDOR_MARKER_DESCRIPTION";
        public static string _POWER_CELL_MARKER_DESCRIPTION => "_POWER_CELL_MARKER_DESCRIPTION";

        private static void Load(On.LocalizationSystem.orig_Init orig)
        {
            orig();
            LocalizationSystem.localizedEN[_LORE_TITLE] = "Lore";
            LocalizationSystem.localizedEN[_LORE_DESCRIPTION] = "The digitised wisdom of past Arcadians.";
            LocalizationSystem.localizedEN[_NOTHING_TITLE] = "Nothing";
            LocalizationSystem.localizedEN[_NOTHING_DESCRIPTION] = "This page intentionally left blank.";
            LocalizationSystem.localizedEN[_HEALTH_MARKER_DESCRIPTION] = "Shows the locations of repair stations on the map.";
            LocalizationSystem.localizedEN[_BANK_MARKER_DESCRIPTION] = "Shows the locations of Goldcrest perches on the map.";
            LocalizationSystem.localizedEN[_TRAIN_MARKER_DESCRIPTION] = "Shows the locations of train stations on the map.";
            LocalizationSystem.localizedEN[_VENDOR_MARKER_DESCRIPTION] = "Shows the locations of various characters on the map.";
            LocalizationSystem.localizedEN[_POWER_CELL_MARKER_DESCRIPTION] = "Shows the locations of power cells on the map.";
        }

        internal static void Hook()
        {
            On.LocalizationSystem.Init += Load;
        }
    }
}