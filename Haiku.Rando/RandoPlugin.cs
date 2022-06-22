using System.Collections;
using BepInEx;
using Haiku.Rando.Checks;
using Haiku.Rando.Topology;
using UnityEngine;

namespace Haiku.Rando
{
    [BepInPlugin("haiku.rando", "Haiku Rando", "1.0.0.0")]
    [BepInDependency("haiku.mapi", "1.0")]
    public sealed class RandoPlugin : BaseUnityPlugin
    {
        public void Start()
        {
            HaikuResources.Init();
            UniversalPickup.InitHooks();
            ShopItemReplacer.InitHooks();
            CheckManager.InitHooks();
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
