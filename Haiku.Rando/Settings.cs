using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Modding;
using Haiku.Rando.Multiworld;

namespace Haiku.Rando
{
    public static class Settings
    {
        public static ConfigEntry<RandomizationLevel> RandoLevel { get; private set; }
        public static ConfigEntry<string> Seed { get; private set; }
        public static ConfigEntry<bool> RandomStartLocation { get; private set; }
        public static ConfigEntry<bool> TrainLoverMode { get; private set; }

        private static List<ConfigEntry<bool>> StartingItemToggles;
        private static List<ConfigEntry<bool>> PoolToggles;
        private static List<ConfigEntry<bool>> SkipToggles;

        public static ConfigEntry<bool> IncludeOldArcadia { get; private set; }
        public static ConfigEntry<bool> IncludeLostArchives { get; private set; }

        public static ConfigEntry<bool> ShowRecentPickups { get; private set; }
        public static ConfigEntry<bool> FastMoney { get; private set; }
        public static ConfigEntry<bool> SyncedMoney { get; private set; }
        public static ConfigEntry<bool> PreBrokenDoors { get; private set; }

        public static ConfigEntry<bool> MWEnabled { get; private set; }
        public static ConfigEntry<string> MWServerAddr { get; private set; }
        public static ConfigEntry<string> MWNickname { get; private set; }
        public static ConfigEntry<string> MWRoomName { get; private set; }

        //Groups of settings
        private const string General = "General";
        private const string Areas = "Special Areas";
        private const string QoL = "QoL";
        private const string Multiworld = "Multiworld";

        private static readonly Regex camelCasePattern = new(@"([a-z])([A-Z])");

        private static T[] EnumValues<T>() => (T[])Enum.GetValues(typeof(T));
        private static string SplitCamelCase(string cc) => camelCasePattern.Replace(cc, "$1 $2");

        public static void Init(ConfigFile config)
        {
            var skipDescriptions = new Dictionary<Skip, string>()
            {
                {Skip.DarkRooms, "Consider most dark rooms traversable without Bulblet"},
                {Skip.HazardRooms, "Consider surface and some hot rooms traversable without their respective sealants"},
                {Skip.SkillChips, "Allow Auto Modifier and Self-Detonation to substitute for Ball and Bomb respectively"}
            };

            RandoLevel = config.Bind(General, "Level", RandomizationLevel.Pickups);
            Seed = config.Bind(General, "Seed", "", "Seed (blank for auto)");
            RandomStartLocation = config.Bind(General, "Random Start Location", false);
            TrainLoverMode = config.Bind(General, "Train Lover Mode", false);

            StartingItemToggles = EnumValues<StartingItemSet>()
                .Select(c => config.Bind("Starting Items", SplitCamelCase(c.ToString()), false))
                .ToList();

            PoolToggles = EnumValues<Pool>()
                .Select(c => config.Bind("Pool", SplitCamelCase(c.ToString()), c != Pool.Wrench))
                .ToList();
            
            SkipToggles = EnumValues<Skip>()
                .Select(c => config.Bind("Skips", SplitCamelCase(c.ToString()), false, skipDescriptions.TryGetValue(c, out var d) ? d : ""))
                .ToList();

            IncludeOldArcadia = config.Bind(Areas, "Old Arcadia", true, "Includes all checks gated by the Old Arcadia door");
            IncludeLostArchives = config.Bind(Areas, "Lost Archives", true, "Includes all checks gated by the Lost Archives door");

            //TODO: Load/Save settings to copyable string
            //TODO: Hash display for race sync
            //ConfigManagerUtil.createButton(config, ShowHash, General, "ShowHash", "Show Hash");

            ShowRecentPickups = config.Bind(QoL, "ShowRecentPickups", true,
                                            "Whether to show a list of recent pickups");
            FastMoney = config.Bind(QoL, "FastMoney", true,
                                    "Makes it so that money totems drop all their money in a single hit");
            SyncedMoney = config.Bind(QoL, "SyncedMoney", true,
                                      "Synchronizes money drop randomization from totems and enemies; intended for racing");
            PreBrokenDoors = config.Bind(QoL, "PreBrokenDoors", true,
                                         "Makes breakable doors automatically break upon spawning; required for a few room rando transitions");
            
            MWServerAddr = config.Bind(Multiworld, "Server Address", "127.0.0.1", "The address or address:port of the multiworld server");
            MWNickname = config.Bind(Multiworld, "Nickname", "", "The nickname other players will see");
            MWRoomName = config.Bind(Multiworld, "Room Name", "", "The name of the room to join");
            ConfigManagerUtil.createButton(config, ReadyMW, Multiworld, "Ready", "Connect to the server and join a room");
            ConfigManagerUtil.createButton(config, DisconnectMW, Multiworld, "Disconnect", "Disconnect from the server");
            ConfigManagerUtil.createButton(config, StartMW, Multiworld, "Start MW", "Begin shuffling items between worlds");
            ConfigManagerUtil.createButton(config, EjectMW, Multiworld, "Eject", "Send out all items belonging to other players");

            //Save defaults if didn't already exist
            config.Save();
        }

        private static void ReadyMW()
        {
            MWConnection.Start();
            MWConnection.Current.Connect(MWServerAddr.Value, MWNickname.Value, MWRoomName.Value);
        }

        private static void DisconnectMW()
        {
            MWConnection.Terminate();
            RandoPlugin.InvokeOnMainThread(rp => rp.ShowMWStatus(""));
        }

        private static void StartMW()
        {
            if (MWConnection.Current != null)
            {
                MWConnection.Current.StartRandomization();
            }
        }

        private static void EjectMW()
        {
            RandoPlugin.InvokeOnMainThread(rp => rp.EjectMW());
        }

        public static GenerationSettings GetGenerationSettings()
        {
            if (RandoLevel.Value == RandomizationLevel.None)
            {
                return null;
            }

            return new()
            {
                Seed = Seed.Value,
                Level = RandoLevel.Value,
                RandomStartLocation = RandomStartLocation.Value,
                TrainLoverMode = TrainLoverMode.Value,
                StartingItems = BitsetFromToggles(StartingItemToggles),
                Pools = BitsetFromToggles(PoolToggles),
                Skips = BitsetFromToggles(SkipToggles),
                IncludeOldArcadia = IncludeOldArcadia.Value,
                IncludeLostArchives = IncludeLostArchives.Value
            };
        }

        private static Bitset64 BitsetFromToggles(List<ConfigEntry<bool>> toggles)
        {
            var s = new Bitset64();
            for (var i = 0; i < toggles.Count; i++)
            {
                if (toggles[i].Value)
                {
                    s.Add(i);
                }
            }
            return s;
        }

        private static void ShowHash()
        {
            //TODO
        }
    }
}
