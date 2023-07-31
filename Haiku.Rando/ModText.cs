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
        public static string _CLOCK_TITLE => "_CLOCK_TITLE";
        public static string _CLOCK_DESCRIPTION => "_CLOCK_DESCRIPTION";
        public static string _LEVER_TITLE(int n) => "_LEVER_TITLE_" + n;
        public static string _LEVER_DESCRIPTION(int n) => "_LEVER_DESCRIPTION_" + n;

        private static void Load(On.LocalizationSystem.orig_Init orig)
        {
            orig();
            var en = LocalizationSystem.localizedEN;
            en[_LORE_TITLE] = "Lore";
            en[_LORE_DESCRIPTION] = "The digitised wisdom of past Arcadians.";
            en[_NOTHING_TITLE] = "Nothing";
            en[_NOTHING_DESCRIPTION] = "This page intentionally left blank.";
            en[_HEALTH_MARKER_DESCRIPTION] = "Shows the locations of repair stations on the map.";
            en[_BANK_MARKER_DESCRIPTION] = "Shows the locations of Goldcrest perches on the map.";
            en[_TRAIN_MARKER_DESCRIPTION] = "Shows the locations of train stations on the map.";
            en[_VENDOR_MARKER_DESCRIPTION] = "Shows the locations of various characters on the map.";
            en[_POWER_CELL_MARKER_DESCRIPTION] = "Shows the locations of power cells on the map.";
            en[_CLOCK_TITLE] = "Clock";
            en[_CLOCK_DESCRIPTION] = "The core clock of Arcadia's transportation systems.";

            en[_LEVER_TITLE(4)] = "Wake Area Lever";
            en[_LEVER_DESCRIPTION(4)] = "The first lever Haiku ever saw, it shall forever have a special place in their core.";
            en[_LEVER_TITLE(6)] = "Abandoned Wastes-Incinerator Lever";
            en[_LEVER_DESCRIPTION(6)] = "Literally opens the gates of Hell.";
            en[_LEVER_TITLE(7)] = "Buzzsaw Lever";
            en[_LEVER_DESCRIPTION(7)] = "Opens a small shortcut to the room where Buzzsaw lives.";
            en[_LEVER_TITLE(9)] = "Magnet Return Lever";
            en[_LEVER_DESCRIPTION(9)] = "Allows machines to get around a large garbage magnet.";
            en[_LEVER_TITLE(12)] = "Upper Incinerator Power Cell Lever";
            en[_LEVER_DESCRIPTION(12)] = "This lever unlocks nothing, but merely makes one's life slightly easier.";
            en[_LEVER_TITLE(13)] = "Incinerator Bridge Lever";
            en[_LEVER_DESCRIPTION(13)] = "Allows safe passage over a large pit of flames.";
            en[_LEVER_TITLE(15)] = "Electron Return Lever";
            en[_LEVER_DESCRIPTION(15)] = "A great timesaver for those who fall to the third Creator.";
            en[_LEVER_TITLE(17)] = "Piston Bridge Lever";
            en[_LEVER_DESCRIPTION(17)] = "Opens the left side entrance to the Steam Town.";
            en[_LEVER_TITLE(20)] = "Bulb Hive Left Lever";
            en[_LEVER_DESCRIPTION(20)] = "Together with its two siblings, opens the way to the Hive.";
            en[_LEVER_TITLE(21)] = "Bulb Hive Middle Lever";
            en[_LEVER_DESCRIPTION(21)] = "Together with its two siblings, opens the way to the Hive.";
            en[_LEVER_TITLE(22)] = "Bulb Hive Right Lever";
            en[_LEVER_DESCRIPTION(22)] = "Opens the way to the Hive.";
            en[_LEVER_TITLE(25)] = "Factory Station Lever";
            en[_LEVER_DESCRIPTION(25)] = "Opens the right side entrance to the Factory Facility train station.";
            en[_LEVER_TITLE(26)] = "Factory Saw Tunnel Lever";
            en[_LEVER_DESCRIPTION(26)] = "Allows entrance to the Factory Facility from the right side without the String and Hook.";
            en[_LEVER_TITLE(27)] = "Neutron Return Lever";
            en[_LEVER_DESCRIPTION(27)] = "Who needs this lever when one has the ability to blink through walls?";
            en[_LEVER_TITLE(28)] = "World Map Room Lever";
            en[_LEVER_DESCRIPTION(28)] = "Opens a backdoor to the monitor room overseeing all of Arcadia.";
            en[_LEVER_TITLE(30)] = "Exposed Pipe Lever";
            en[_LEVER_DESCRIPTION(30)] = "Situated near the pipe that waters the overgrown ruins underneath the core of Arcadia.";
            en[_LEVER_TITLE(37)] = "Coolant Soluble Lever";
            en[_LEVER_DESCRIPTION(37)] = "Situated near the Coolant Soluble chip. Unlocks nothing.";
            en[_LEVER_TITLE(38)] = "Sunken Key Lever";
            en[_LEVER_DESCRIPTION(38)] = "Situated near a Rusted Key and the Amplifying Transputer chip.";
            en[_LEVER_TITLE(42)] = "Steam Town Engine Lever";
            en[_LEVER_DESCRIPTION(42)] = "The only purpose of this is to cut off an infinite loop in the engine of Steam Town.";
            en[_LEVER_TITLE(44)] = "Factory Interlocked Lever A";
            en[_LEVER_DESCRIPTION(44)] = "The first of two levers that must be unlocked to advance deeper into the Factory Facility.";
            en[_LEVER_TITLE(45)] = "Factory Interlocked Lever 02";
            en[_LEVER_DESCRIPTION(45)] = "The second of two levers that must be unlocked to advance deeper into the Factory Facility.";
            en[_LEVER_TITLE(46)] = "Splunk's Home Lever";
            en[_LEVER_DESCRIPTION(46)] = "Notable only for being located near the new home of a toaster.";
            en[_LEVER_TITLE(47)] = "First Tree Lever";
            en[_LEVER_DESCRIPTION(47)] = "Opens a backdoor to the First Tree of Arcadia.";
            en[_LEVER_TITLE(48)] = "Derelict Hole Lever";
            en[_LEVER_DESCRIPTION(48)] = "Opens the right side entrance to the Derelict Hole.";
            en[_LEVER_TITLE(49)] = "Post Car Battery Lever";
            en[_LEVER_DESCRIPTION(49)] = "Opens an easier way to Pinion's Expanse, with far lower risk of electrocution.";
            en[_LEVER_TITLE(51)] = "Furnace Lever";
            en[_LEVER_DESCRIPTION(51)] = "Allows an easy and safe return from the map disruptor in the Blazing Furnace.";
            en[_LEVER_TITLE(53)] = "Research Lab Lever";
            en[_LEVER_DESCRIPTION(53)] = "Opens an alternative entrance to the Research Lab.";
            en[_LEVER_TITLE(54)] = "Snailbot Burrow Lever";
            en[_LEVER_DESCRIPTION(54)] = "Opens a gate leading to the darkest depths of Arcadia's water ducts.";
            en[_LEVER_TITLE(55)] = "Sealants Lever";
            en[_LEVER_DESCRIPTION(55)] = "Opens a way to the only sealant application machines still functioning in Arcadia.";
            en[_LEVER_TITLE(56)] = "Mischevious Lever";
            en[_LEVER_DESCRIPTION(56)] = "Legend has it that this lever is sentient, and spends its time pranking the other inhabitants of Arcadia.";
            
        }

        internal static void Hook()
        {
            On.LocalizationSystem.Init += Load;
        }
    }
}