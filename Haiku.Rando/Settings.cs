﻿using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using Modding;

namespace Haiku.Rando
{
    public static  class Settings
    {
        public static ConfigEntry<RandomizationLevel> RandoLevel { get; private set; }
        public static ConfigEntry<string> Seed { get; private set; }

        public static ConfigEntry<bool> IncludeWrench { get; private set; }
        public static ConfigEntry<bool> IncludeBulblet { get; private set; }
        public static ConfigEntry<bool> IncludeAbilities { get; private set; }
        public static ConfigEntry<bool> IncludeItems { get; private set; }
        public static ConfigEntry<bool> IncludeChips { get; private set; }
        public static ConfigEntry<bool> IncludeChipSlots { get; private set; }
        public static ConfigEntry<bool> IncludeMapDisruptors { get; private set; }
        public static ConfigEntry<bool> IncludeLevers { get; private set; }
        public static ConfigEntry<bool> IncludePowerCells { get; private set; }
        public static ConfigEntry<bool> IncludeCoolant { get; private set; }
        public static ConfigEntry<bool> IncludeSealants { get; private set; }

        //QoL: Fast Money
        //QoL: Normalized Money Drops

        //Groups of settings
        private const string General = "General";
        private const string Pool = "Pool";
        private const string QoL = "QoL";

        public static void Init(ConfigFile config)
        {
            RandoLevel = config.Bind(General, "Level", RandomizationLevel.Pickups);
            Seed = config.Bind(General, "Seed", "", "Seed (blank for auto)");
            //TODO: Load/Save settings to copyable string
            //TODO: Hash display for race sync
            //ConfigManagerUtil.createButton(config, ShowHash, General, "ShowHash", "Show Hash");

            IncludeWrench = config.Bind(Pool, "Wrench", false);
            IncludeBulblet = config.Bind(Pool, "Bulblet", true);
            IncludeAbilities = config.Bind(Pool, "Abilities", true);
            IncludeItems = config.Bind(Pool, "Items", true);
            IncludeChips = config.Bind(Pool, "Chips", true);
            IncludeChipSlots = config.Bind(Pool, "Chip Slots", true);
            IncludeMapDisruptors = config.Bind(Pool, "Map Disruptors", true);
            IncludeLevers = config.Bind(Pool, "Levers", false);
            IncludePowerCells = config.Bind(Pool, "Power Cells", true);
            IncludeCoolant = config.Bind(Pool, "Coolant", true);
            IncludeSealants = config.Bind(Pool, "Sealants", true);

            //Save defaults if didn't already exist
            config.Save();
        }

        private static void ShowHash()
        {
            //TODO
        }
    }
}
