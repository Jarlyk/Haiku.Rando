using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using Haiku.Rando.Checks;
using Haiku.Rando.Logic;
using Haiku.Rando.Topology;
using Haiku.Rando.Util;
using MonoMod.Cil;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haiku.Rando
{
    [BepInPlugin("haiku.rando", "Haiku Rando", "0.1.0.0")]
    [BepInDependency("haiku.mapi", "1.0")]
    public sealed class RandoPlugin : BaseUnityPlugin
    {
        private RandoTopology _topology;
        private LogicLayer _baseLogic;
        private CheckRandomizer _randomizer;
        private TransitionRandomizer _transRandomizer;
        private ulong? _savedSeed;

        public void Start()
        {
            Settings.Init(Config);

            HaikuResources.Init();
            UniversalPickup.InitHooks();
            ShopItemReplacer.InitHooks();
            CheckManager.InitHooks();
            TransitionManager.InitHooks();
            QoL.InitHooks();
            RepairStationWarp.InitHooks();

            IL.LoadGame.Start += LoadGame_Start;
            On.PCSaveManager.Load += PCSaveManager_Load;
            On.PCSaveManager.Save += PCSaveManager_Save;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            //TODO: Add bosses as logic conditions
            //This impacts some transitions
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
                _baseLogic = LogicLayer.Deserialize(_topology, reader);
            }

        }

        private void PCSaveManager_Load(On.PCSaveManager.orig_Load orig, PCSaveManager self, string filePath)
        {
            orig(self, filePath);
            _savedSeed = null;
            var hasRandoData = self.es3SaveFile.Load<bool>("hasRandoData", false);
            if (hasRandoData)
            {
                _savedSeed = self.es3SaveFile.Load<ulong>("randoSeed", 0UL);
            }
        }

        private void PCSaveManager_Save(On.PCSaveManager.orig_Save orig, PCSaveManager self, string filePath)
        {
            orig(self, filePath);
            if (_savedSeed != null && Settings.RandoLevel.Value != RandomizationLevel.None)
            {
                self.es3SaveFile.Save("hasRandoData", true);
                self.es3SaveFile.Save("randoSeed", _savedSeed.Value);
                self.es3SaveFile.Sync();
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

            var level = Settings.RandoLevel.Value;
            if (level != RandomizationLevel.None)
            {
                ReloadTopology();
                _savedSeed = GetSeed(_savedSeed);
                if (level == RandomizationLevel.Rooms)
                {
                    Debug.Log("** Configuring transition randomization **");
                    _transRandomizer = new TransitionRandomizer(_topology, _savedSeed.Value);
                    _transRandomizer.Randomize();
                    TransitionManager.Instance.Randomizer = _transRandomizer;
                }

                Debug.Log("** Configuring check randomization **");
                var evaluator = new LogicEvaluator(new[] { _baseLogic });
                _randomizer = new CheckRandomizer(_topology, evaluator, _savedSeed.Value);
                _savedSeed = _randomizer.Seed;
                _randomizer.Randomize();
                CheckManager.Instance.Randomizer = _randomizer;
                Debug.Log("** Randomization complete **");
            }
        }

        private ulong GetSeed(ulong? savedSeed)
        {
            ulong seed;
            if (savedSeed != null)
            {
                seed = savedSeed.Value;
            }
            else if (string.IsNullOrEmpty(Settings.Seed.Value))
            {
                var tempRandom = new Xoroshiro128Plus();
                seed = tempRandom.NextULong();
            }
            else if (!ulong.TryParse(Settings.Seed.Value, out seed))
            {
                seed = (ulong)((long)Settings.Seed.Value.GetHashCode() - int.MinValue);
            }

            return seed;
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CheckManager.Instance.OnSceneLoaded(scene.buildIndex);
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
