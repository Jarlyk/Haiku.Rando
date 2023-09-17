using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using SysDiag = System.Diagnostics;
using System.Linq;
using BepInEx;
using Haiku.Rando.Checks;
using Haiku.Rando.Logic;
using Haiku.Rando.Topology;
using Haiku.Rando.UI;
using Haiku.Rando.Util;
using Haiku.Rando.Multiworld;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MMDetour = MonoMod.RuntimeDetour;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haiku.Rando
{
    [BepInPlugin("haiku.rando", "Haiku Rando", "2.2.3.0")]
    [BepInDependency("haiku.mapi", "1.0")]
    public sealed class RandoPlugin : BaseUnityPlugin
    {
        private RandoTopology _topology;
        private CheckRandomizer _randomizer;
        private TransitionRandomizer _transRandomizer;

        private SaveData _saveData;
        private SaveData _presetSaveData;

        private readonly static ConcurrentQueue<Action<RandoPlugin>> MainThreadCallbacks = new();

        internal static void InvokeOnMainThread(Action f)
        {
            MainThreadCallbacks.Enqueue(_ => f());
        }

        internal static void InvokeOnMainThread(Action<RandoPlugin> f)
        {
            MainThreadCallbacks.Enqueue(f);
        }

        internal CheckRandomizer Randomizer => _randomizer;

        public void Start()
        {
            Settings.Init(Config);

            HaikuResources.Init();
            UniversalPickup.InitHooks();
            QuaternRewardReplacer.InitHooks();
            MotherRewardReplacer.InitHooks();
            BeeHiveItemReplacer.InitHooks();
            SealantShopItemReplacer.InitHooks();
            RustyItemReplacer.InitHooks();
            ShopItemReplacer.InitHooks();
            ClockRepairReplacer.InitHooks();
            LeverReplacer.InitHooks();
            CheckManager.Instance.InitHooks(Logger.Log, () => _saveData);
            TransitionManager.InitHooks();
            QoL.InitHooks();
            ModText.Hook();
            
            IL.LoadGame.Start += LoadGame_Start;
            On.PCSaveManager.Load += PCSaveManager_Load;
            On.PCSaveManager.Save += PCSaveManager_Save;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            // prevent CheckChipsWhenGameStarts and friends from giving unexpected chips, keys
            // and coolant when warping
            On.ReplenishHealth.CheckChipsWhenGameStarts += NoCheckChips;
            On.ReplenishHealth.CheckKeysWhenGameStarts += NoCheckKeys;
            new MMDetour.Hook(typeof(ReplenishHealth).GetMethod("CheckCoolantWhenGameStarts", BindingFlags.Instance | BindingFlags.NonPublic), NoCheckCoolant);

            // for Train Lover Mode
            IL.ReplenishHealth.Update += StickToTrain;
            On.TalkToTrainConductor.AssignFirstItemToEvents += EnableAltFirstStation;

            // disable the Archives cutscene trigger in the boss rush if it is entered before the Creator trio
            // fight (possible with lever rando)
            // if the trigger is activated, it warps the player out of the boss rush, to the real Archives.
            On.TheArchivesCutscene.Start += DisableArchivesCutsceneInBossRush;

            //TODO: Add bosses as logic conditions
            //This impacts some transitions

            gameObject.AddComponent<RecentPickupDisplay>();
        }

        private void NoCheckChips(On.ReplenishHealth.orig_CheckChipsWhenGameStarts orig, ReplenishHealth self)
        {
            if (_randomizer == null) orig(self);
        }

        private void NoCheckCoolant(Action<ReplenishHealth> orig, ReplenishHealth self)
        {
            if (_randomizer == null) orig(self);
        }

        private void NoCheckKeys(On.ReplenishHealth.orig_CheckKeysWhenGameStarts orig, ReplenishHealth self)
        {
            if (_randomizer == null) orig(self);
        }

        private void StickToTrain(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(i => i.MatchStfld(typeof(GameManager), "savePointSceneIndex")))
            {
                Logger.LogWarning("StickToTrain patch failed; Train Lover Mode will allow respawn point to be set anywhere");
                return;
            }
            c.EmitDelegate((Func<int, int>)(room => _randomizer != null && _randomizer.Settings.TrainLoverMode ? SpecialScenes.Train : room));
        }

        private IEnumerator EnableAltFirstStation(On.TalkToTrainConductor.orig_AssignFirstItemToEvents orig, TalkToTrainConductor self)
        {
            if (_randomizer != null && _randomizer.Settings.Contains(Pool.Clock))
            {
                self.firstLocation = SceneUtils.FindObjectsOfType<FastTravelButton>()
                    .Where(b => GameManager.instance.trainStations[b.fastTravelSaveID].unlockedStation)
                    .Select(b => b.gameObject)
                    .FirstOrDefault();
            }
            return orig(self);
        }

        private static void DisableArchivesCutsceneInBossRush(On.TheArchivesCutscene.orig_Start orig, TheArchivesCutscene self)
        {
            orig(self);
            if (BossRushMode.instance is {} brm && (brm.bossRushIsActive || brm.bossRushSelectIsActive))
            {
                self.dialogueTrigger.SetActive(false);
            }
        }

        internal void ReloadTopology()
        {
            using (var stream = Assembly.GetExecutingAssembly()
                                        .GetManifestResourceStream("Haiku.Rando.Resources.HaikuTopology.json"))
            {
                _topology = RandoTopology.Deserialize(new StreamReader(stream));
            }
        }

        private LogicLayer LoadLogicLayer(string name, Func<Skip, bool> enabledSkips)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Haiku.Rando.Resources.{name}.txt");
            using var reader = new StreamReader(stream);
            return LogicLayer.Deserialize(_topology, enabledSkips, reader);
        }

        private void PCSaveManager_Load(On.PCSaveManager.orig_Load orig, PCSaveManager self, string filePath)
        {
            orig(self, filePath);
            try
            {
                _saveData = SaveData.Load(self.es3SaveFile);
            }
            catch (Exception err)
            {
                Logger.LogError(err.ToString());
            }
        }

        private void PCSaveManager_Save(On.PCSaveManager.orig_Save orig, PCSaveManager self, string filePath)
        {
            orig(self, filePath);
            try
            {
                if (_saveData != null)
                {
                    _saveData.SaveTo(self.es3SaveFile);
                }
            }
            catch (Exception err)
            {
                Logger.LogError(err.ToString());
            }
        }

        private void LoadGame_Start(ILContext il)
        {
            var c = new ILCursor(il);
            c.GotoNext(MoveType.After, i => i.MatchCallvirt("GameManager", "LoadSaveFile"));
            c.EmitDelegate((Action)BeginRando);
        }

        private void BeginRando()
        {
            _randomizer = null;
            _transRandomizer = null;
            CheckManager.Instance.Randomizer = null;
            TransitionManager.Instance.Randomizer = null;

            // Add rando save data to the file if it does not already have it, and it's a
            // newly-started file (which introPlayed is a proxy for).
            if (_saveData == null && !GameManager.instance.introPlayed)
            {
                if (_presetSaveData != null)
                {
                    _saveData = _presetSaveData;
                    _presetSaveData = null;
                }
                else if (Settings.GetGenerationSettings() is GenerationSettings s)
                {
                    if (string.IsNullOrWhiteSpace(s.Seed))
                    {
                        s.Seed = DateTime.Now.Ticks.ToString();
                    }
                    _saveData = new(s);
                }
            }
            var gs = _saveData?.Settings;

            bool success = false;
            if (gs != null && gs.Level != RandomizationLevel.None)
            {
                int? startScene = SpecialScenes.GameStart;

                var timer = new SysDiag.Stopwatch();
                timer.Start();

                // A previous room rando may have rewired the topology; make sure we start with the
                // vanilla topology.
                ReloadTopology();
                if (RetryRandomize(gs, out startScene))
                {
                    success = true;
                }
                else
                {
                    Debug.LogWarning($"** Failed to complete Randomization after all allowed attempts; it's possible the settings may not allow for completion **");
                }

                timer.Stop();
                Debug.Log($"completed randomization in {timer.ElapsedMilliseconds} ms");

                GiveStartingState();

                if (_saveData.MW != null)
                {
                    var mw = _saveData.MW;
                    MWConnection.Join(mw.ServerAddr, mw.PlayerId, mw.RandoId, mw.SelfNickname);
                    mw.ApplyText();
                    mw.ApplyPlacements(_randomizer);
                }
            }
            else
            {
                success = true;
            }

            if (!success)
            {
                //Go back to main menu if failed
                GameManager.instance.introPlayed = true;
                GameManager.instance.savePointSceneIndex = 0;
            }
        }

        internal SaveData InitSaveData(GenerationSettings gs)
        {
            _presetSaveData = new(gs);
            return _presetSaveData;
        }

        internal bool GiveCheck(int i)
        {
            if (_randomizer == null)
            {
                return false;
            }
            var allChecks = _randomizer.Topology.Checks;
            if (!(i >= 0 && i < allChecks.Count))
            {
                return false;
            }
            CheckManager.TriggerCheck(null, allChecks[i]);
            return true;
        }

        internal bool ConfirmRemoteCheck(string name, int playerId)
        {
            if (_saveData == null || _saveData.MW == null)
            {
                return false;
            }
            foreach (var ri in _saveData.MW.RemoteItems)
            {
                if (ri.Name == name && ri.PlayerId == playerId)
                {
                    ri.State = RemoteItemState.Confirmed;
                    return true;
                }
            }
            return false;
        }

        internal int MWPlayerId() =>
            _saveData != null && _saveData.MW != null ? _saveData.MW.PlayerId : -1;

        internal void GiveStartingState()
        {
            var rando = _randomizer;
            var gs = rando.Settings;
            if (rando.StartScene != null)
            {
                GameManager.instance.introPlayed = true;
                GameManager.instance.savePointSceneIndex = rando.StartScene.Value;
            }

            if (gs.Contains(StartingItemSet.Wrench) && !GameManager.instance.canHeal)
            {
                GameManager.instance.canHeal = true;
                InventoryManager.instance.AddItem((int)ItemId.Wrench);
            }

            if (gs.Contains(StartingItemSet.Whistle) && !CheckManager.HasItem(ItemId.Whistle))
            {
                InventoryManager.instance.AddItem((int)ItemId.Whistle);
            }

            if (gs.Contains(StartingItemSet.Maps))
            {
                // The following is directly copied from DebugMod's GiveAllMaps.
                for (int i = 0; i < GameManager.instance.mapTiles.Length; i++)
                {
                    GameManager.instance.mapTiles[i].explored = true;
                }
                for (int j = 0; j < GameManager.instance.disruptors.Length; j++)
                {
                    GameManager.instance.disruptors[j].destroyed = true;
                }
            }

            if (_randomizer.StartSpareParts > 0)
            {
                InventoryManager.instance.AddSpareParts(_randomizer.StartSpareParts);
            }

            if (gs.TrainLoverMode && _randomizer.StartStation is int startStation)
            {
                GameManager.instance.trainUnlocked = true;
                GameManager.instance.trainStations[startStation].unlockedStation = true;
                GameManager.instance.trainRoom = startStation switch
                {
                    0 => 28,
                    1 => 88,
                    2 => 179,
                    3 => 53,
                    4 => 209,
                    5 => 136,
                    6 => 67,
                    7 => 146,
                    _ => throw new ArgumentOutOfRangeException($"unknown room for station {_randomizer.StartStation}")
                };
            }
        }

        private LogicEvaluator LoadLogic(GenerationSettings gs)
        {
            var baseLogic = LoadLogicLayer("BaseLogic", gs.Contains);
            var logicLayers = new List<LogicLayer>() { baseLogic };
            if (gs.Contains(Skip.EnemyPogos)) logicLayers.Add(LoadLogicLayer("EnemyPogoLogic", gs.Contains));
            if (gs.Contains(Skip.BLJ)) logicLayers.Add(LoadLogicLayer("BLJLogic", gs.Contains));
            if (gs.Contains(Skip.BombJumps)) logicLayers.Add(LoadLogicLayer("BombJumpLogic", gs.Contains));
            if (gs.Contains(Skip.SkillChips)) logicLayers.Add(LoadLogicLayer("SkillChipLogic", gs.Contains));
            if (gs.Contains(Skip.DoubleJumpChains)) logicLayers.Add(LoadLogicLayer("DoubleJumpChainLogic", gs.Contains));
            // See the hazard room logic file for why this randomization level check is needed.
            if (gs.Level == RandomizationLevel.Pickups && gs.Contains(Skip.HazardRooms))
            {
                logicLayers.Add(LoadLogicLayer("HazardRoomLogic", gs.Contains));
            }

            return new(logicLayers);
        }

        internal bool RetryRandomize(GenerationSettings gs, out int? startScene)
        {
            const int maxRetries = 200;

            var eval = LoadLogic(gs);
            var origSeed = gs.Seed;
            for (int i = 0; i < maxRetries; i++)
            {
                if (TryRandomize(gs, eval, out startScene))
                {
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Randomization failed: attempt {i+1} of {maxRetries}");
                    //Iterate the seed
                    gs.Seed = $"{origSeed}__attempt{i+1}";
                    if (gs.Level == RandomizationLevel.Rooms)
                    {
                        ReloadTopology();
                    }
                }
            }
            startScene = null;
            return false;
        }

        private bool TryRandomize(GenerationSettings gs, LogicEvaluator evaluator, out int? startScene)
        {
            

            var seed = new Seed128(gs.Seed);

            if (gs.TrainLoverMode)
            {
                startScene = SpecialScenes.Train;
            }
            else if (gs.RandomStartLocation)
            {
                // Pick from any save station except Incinerator, Furnace, Train
                // and Old Arcadia
                var availScenes = new List<int>
                    { 10, 15, 21, 71, 57, 41, 75, 195, 172, 194, 87, 113, 139, 140, 167 };

                if (gs.Contains(Skip.DarkRooms))
                {
                    availScenes.Add(161);
                    availScenes.Add(156);
                    availScenes.Add(127);
                }

                var tmpRandom = new Xoroshiro128Plus(seed.S0, seed.S1);
                startScene = availScenes[tmpRandom.NextRange(0, availScenes.Count)];
            }
            else
            {
                startScene = null;
            }

            if (gs.Level == RandomizationLevel.Rooms)
            {
                Debug.Log("** Configuring transition randomization **");
                _transRandomizer = new TransitionRandomizer(_topology, evaluator, seed);
                _transRandomizer.Randomize();
                TransitionManager.Instance.Randomizer = _transRandomizer;
            }

            Debug.Log("** Configuring check randomization **");
            _randomizer = CheckRandomizer.TryRandomize(_topology, evaluator, gs, seed, startScene);
            if (_randomizer == null)
            {
                return false;
            }
            CheckManager.Instance.Randomizer = _randomizer;
            Debug.Log("** Randomization complete **");
            return true;
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CheckManager.Instance.OnSceneLoaded(scene.buildIndex);

            for (var i = 0; i < InventoryManager.instance.items.Length; i++)
            {
                Debug.Log($"Item {i} name = {InventoryManager.instance.items[i].itemName}");
            }

            if (_saveData != null && _saveData.Settings.Level == RandomizationLevel.Rooms)
            {
                //In room rando, all rooms can disable fire
                var detector = FindObjectOfType<FireResDetector>();
                if (!detector)
                {
                    var detectorObj = new GameObject();
                    detector = detectorObj.AddComponent<FireResDetector>();
                    detector.disableHeat = true;
                }
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Y) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                StartCoroutine(RunMapping());
            }

            while (MainThreadCallbacks.TryDequeue(out var f))
            {
                try
                {
                    f(this);
                }
                catch (Exception err)
                {
                    Debug.Log(err.ToString());
                }
            }
        }

        private IEnumerator RunMapping()
        {
            var scanner = new RandoMapScanner();
            return scanner.RunScan();
        }
    }
}
