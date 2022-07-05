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
    [BepInPlugin("haiku.rando", "Haiku Rando", "1.0.0.0")]
    [BepInDependency("haiku.mapi", "1.0")]
    public sealed class RandoPlugin : BaseUnityPlugin
    {
        private RandoTopology _topology;
        private LogicLayer _baseLogic;
        private CheckRandomizerConfig _randoConfig;
        private CheckRandomizer _randomizer;
        private ulong _primarySeed;

        public void Start()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Haiku.Rando.Resources.HaikuTopology.json"))
            {
                _topology = RandoTopology.Deserialize(new StreamReader(stream));
            }
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Haiku.Rando.Resources.BaseLogic.txt"))
            using (var reader = new StreamReader(stream))
            {
                _baseLogic = LogicLayer.Deserialize(_topology, reader);
            }

            _randoConfig = new CheckRandomizerConfig();
            _randoConfig.Seed = new Xoroshiro128Plus().NextULong();

            HaikuResources.Init();
            UniversalPickup.InitHooks();
            ShopItemReplacer.InitHooks();
            CheckManager.InitHooks();
            QoL.InitHooks();

            IL.LoadGame.Start += LoadGame_Start;
            On.PCSaveManager.Load += PCSaveManager_Load;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            //TODO: Add bosses as logic conditions
            //This impacts some transitions
        }

        private void PCSaveManager_Load(On.PCSaveManager.orig_Load orig, PCSaveManager self, string filePath)
        {
            orig(self, filePath);
            _primarySeed = 0;
            var hasRandoData = self.es3SaveFile.Load<bool>("hasRandoData", false);
            if (hasRandoData)
            {
                _primarySeed = self.es3SaveFile.Load<ulong>("randoSeed", 0UL);
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
            if (_primarySeed != 0)
            {
                _randoConfig.Seed = _primarySeed;
            }

            var evaluator = new LogicEvaluator(new[] { _baseLogic });
            _randomizer = new CheckRandomizer(_randoConfig, _topology, evaluator);
            _randomizer.Randomize();
            CheckManager.Instance.Randomizer = _randomizer;
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
