using System;
using System.Linq;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class RustyItemReplacer : MonoBehaviour
    {
        private RandoCheck replacement;

        public static void InitHooks()
        {
            On.Rusty.GiveMarker += GiveReplacementItem;
        }

        private static void GiveReplacementItem(On.Rusty.orig_GiveMarker orig, Rusty self)
        {
            var replacer = self.GetComponent<RustyItemReplacer>();
            if (replacer == null)
            {
                orig(self);
            }
            else
            {
                CheckManager.TriggerCheck(self, replacer.replacement);
                // the one part from the original we want to keep
                if (self.isNote)
                {
                    self.gameObject.SetActive(false);
                }
            }
        }

        public static void ReplaceCheck(RustyType origType, RandoCheck replacement)
        {
            var rusty = SceneUtils.FindObjectsOfType<Rusty>().First(Selector(origType));
            var replacer = rusty.gameObject.AddComponent<RustyItemReplacer>();
            replacer.replacement = replacement;
        }

        private static Func<Rusty, bool> Selector(RustyType t) => t switch
        {
            RustyType.Health => r => r.health,
            RustyType.Train => r => r.train,
            RustyType.Vendor => r => r.vendor,
            RustyType.Bank => r => r.bank,
            RustyType.PowerCell => r => r.powercell,
            _ => r => false
        };
    }
}