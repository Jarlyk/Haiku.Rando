using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using BepInEx;
using Haiku.Rando.Checks;
using Haiku.Rando.Logic;
using Haiku.Rando.Topology;
using Haiku.Rando.UI;
using Haiku.Rando.Util;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haiku.Rando
{
    [BepInPlugin("haiku.rando", "Haiku Rando", "0.4.0.0")]
    [BepInDependency("haiku.mapi", "1.0")]
    public sealed class RandoPlugin : BaseUnityPlugin
    {
        private RandoTopology _topology;
        private LogicLayer _baseLogic;
        private CheckRandomizer _randomizer;
        private TransitionRandomizer _transRandomizer;

        private SaveData _saveData;

        public void Start()
        {
            Settings.Init(Config);

            HaikuResources.Init();
            UniversalPickup.InitHooks();
            ShopItemReplacer.InitHooks();
            CheckManager.Instance.InitHooks(Logger.Log, () => _saveData);
            TransitionManager.InitHooks();
            QoL.InitHooks();
            ModText.Hook();
            
            IL.LoadGame.Start += LoadGame_Start;
            On.PCSaveManager.Load += PCSaveManager_Load;
            On.PCSaveManager.Save += PCSaveManager_Save;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            // prevent CheckChipsWhenGameStarts from giving unexpected chips
            On.ReplenishHealth.CheckChipsWhenGameStarts += WarnCheckChips;

            //TODO: Add bosses as logic conditions
            //This impacts some transitions

            gameObject.AddComponent<RecentPickupDisplay>();
        }

        private void WarnCheckChips(On.ReplenishHealth.orig_CheckChipsWhenGameStarts orig, ReplenishHealth self)
        {
            if (_randomizer == null) orig(self);
        }

        private void ReloadTopology()
        {
            using (var stream = Assembly.GetExecutingAssembly()
                                        .GetManifestResourceStream("Haiku.Rando.Resources.HaikuTopology.json"))
            {
                _topology = RandoTopology.Deserialize(new StreamReader(stream));
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Haiku.Rando.Resources.BaseLogic.txt"))
            using (var reader = new StreamReader(stream))
            {
                var logic = LogicLayer.Deserialize(_topology, reader);
                if (logic == null)
                {
                    Logger.LogError("failed to parse logic");
                }
                else
                {
                    _baseLogic = logic;
                }
            }

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

            bool success = false;
            var level = Settings.RandoLevel.Value;
            if (level != RandomizationLevel.None)
            {
                if (_saveData == null)
                {
                    _saveData = new(PickSeed());
                }

                int? startScene = SpecialScenes.GameStart;
                const int maxRetries = 200;
                for (int i = 0; i < maxRetries; i++)
                {
                    if (TryRandomize(level, out startScene))
                    {
                        success = true;
                        break;
                    }
                    else
                    {
                        Debug.LogWarning($"Randomization failed: attempt {i+1} of {maxRetries}");

                        //Iterate the seed
                        _saveData.Seed = new Xoroshiro128Plus(_saveData.Seed).NextULong();
                    }
                }

                if (!success)
                {
                    Debug.LogWarning($"** Failed to complete Randomization after all allowed attempts; it's possible the settings may not allow for completion **");
                }

                if (startScene != null)
                {
                    GameManager.instance.introPlayed = true;
                    GameManager.instance.savePointSceneIndex = startScene.Value;
                }

            }
            else
            {
                success = true;
            }

            if (Settings.StartWithWrench.Value && !GameManager.instance.canHeal)
            {
                GameManager.instance.canHeal = true;
                InventoryManager.instance.AddItem((int)ItemId.Wrench);
            }

            if (Settings.StartWithWhistle.Value && !CheckManager.HasItem(ItemId.Whistle))
            {
                InventoryManager.instance.AddItem((int)ItemId.Whistle);
            }

            if (Settings.StartWithMaps.Value)
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

                //Turn on all map markers
                GameManager.instance.showPowercells = true;
                GameManager.instance.showHealthStations = true;
                GameManager.instance.showBankStations = true;
                GameManager.instance.showVendors = true;
                GameManager.instance.showTrainStations = true;
            }

            if (!success)
            {
                //Go back to main menu if failed
                GameManager.instance.introPlayed = true;
                GameManager.instance.savePointSceneIndex = 0;
            }
        }

        private bool TryRandomize(RandomizationLevel level, out int? startScene)
        {
            ReloadTopology();

            if (Settings.RandomStartLocation.Value)
            {
                //Pick from any save station except Incinerator, Furnace and Train
                var availScenes = new List<int>
                    { 10, 15, 21, 71, 57, 41, 75, 195, 172, 194, 87, 113, 127, 139, 140, 161, 156, 167 };

                //TODO: We need to remove dark areas, though these could be allowed if dark room skips are enabled
                availScenes.Remove(161);
                availScenes.Remove(156);
                availScenes.Remove(127);

                // Xoroshiro128Plus(UInt64) already mixes the seed bits, so further preprocessing
                // is not required.
                var tmpRandom = new Xoroshiro128Plus(_saveData.Seed);
                startScene = availScenes[tmpRandom.NextRange(0, availScenes.Count)];
            }
            else
            {
                startScene = null;
            }

            var evaluator = new LogicEvaluator(new[] { _baseLogic });

            if (level == RandomizationLevel.Rooms)
            {
                Debug.Log("** Configuring transition randomization **");
                _transRandomizer = new TransitionRandomizer(_topology, evaluator, _saveData.Seed);
                _transRandomizer.Randomize();
                TransitionManager.Instance.Randomizer = _transRandomizer;
            }

            Debug.Log("** Configuring check randomization **");
            _randomizer = new CheckRandomizer(_topology, evaluator, _saveData.Seed, startScene);
            bool success = _randomizer.Randomize();

            if (success)
            {
                CheckManager.Instance.Randomizer = _randomizer;
                Debug.Log("** Randomization complete **");
            }

            return success;
        }

        private ulong PickSeed()
        {
            if (ulong.TryParse(Settings.Seed.Value ?? "", out var seed))
            {
                return seed;
            }
            return new Xoroshiro128Plus().NextULong();
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CheckManager.Instance.OnSceneLoaded(scene.buildIndex);

            if (Settings.RandoLevel.Value == RandomizationLevel.Rooms)
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
        }

        private IEnumerator RunMapping()
        {
            var scanner = new RandoMapScanner();
            return scanner.RunScan();
        }
    }
}
