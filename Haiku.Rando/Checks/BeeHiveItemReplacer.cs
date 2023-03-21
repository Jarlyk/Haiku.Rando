using System;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class BeeHiveItemReplacer : MonoBehaviour
    {
        private RandoCheck replacement;
        private GameObject newPickup;

        public static void InitHooks()
        {
            On.BeeHive.Start += OnStart;
            On.BeeHive.TriggerBulbItem += ReplaceBulbPickup;
        }

        private static void OnStart(On.BeeHive.orig_Start orig, BeeHive self)
        {
            var replacer = self.GetComponent<BeeHiveItemReplacer>();
            if (replacer != null && 
                !GameManager.instance.bosses[self.bossID].defeated &&
                !CheckManager.AlreadyGotCheck(replacer.replacement))
            {
                self.bulbObject.SetActive(false);
            }
            orig(self);
        }

        private static void ReplaceBulbPickup(On.BeeHive.orig_TriggerBulbItem orig, BeeHive self)
        {
            var replacer = self.GetComponent<BeeHiveItemReplacer>();
            if (replacer != null)
            {
                self.bulbPickup = replacer.newPickup;
            }
            orig(self);
        }

        public static void ReplaceCheck(RandoCheck replacement, GameObject pickup)
        {
            var hive = SceneUtils.FindObjectOfType<BeeHive>();
            if (hive == null)
            {
                throw new InvalidOperationException("attempted to replace Bulblet check while Beehive is not present");
            }
            var replacer = hive.gameObject.AddComponent<BeeHiveItemReplacer>();
            replacer.replacement = replacement;
            replacer.newPickup = pickup;
        }
    }
}