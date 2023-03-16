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
            }
        }

        public static void ReplaceCheck(RustyType origType, RandoCheck replacement)
        {
            foreach (var rusty in SceneUtils.FindObjectsOfType<Rusty>())
            {
                if (Matches(origType, rusty))
                {
                    var replacer = rusty.gameObject.AddComponent<RustyItemReplacer>();
                    replacer.replacement = replacement;
                    // The Rusty near the hand monument is coded to disappear if any of the
                    // other Rusties are visited. Not good when we have them randomized.
                    if (origType == RustyType.Health)
                    {
                        foreach (var disabler in rusty.GetComponents<EnableIfNPC>())
                        {
                            // 37 is this Rusty's own NPC number.
                            if (disabler.nPCSaveNumber != 37)
                            {
                                GameObject.Destroy(disabler);
                            }
                        }
                        // Disable the shiny that would normally appear when this Rusty was missed.
                        foreach (var pickup in SceneUtils.FindObjectsOfType<PickupItem>())
                        {
                            if (pickup.triggerPin)
                            {
                                pickup.gameObject.SetActive(false);
                            }
                        }
                    }
                }
                else if (rusty.isNote)
                {
                    rusty.gameObject.SetActive(false);
                }
            }
        }

        private static bool Matches(RustyType t, Rusty r) => t switch
        {
            RustyType.Health => r.health,
            RustyType.Train => r.train,
            RustyType.Vendor => r.vendor,
            RustyType.Bank => r.bank,
            RustyType.PowerCell => r.powercell && !r.isNote && !r.lastEncounter,
            _ => false
        };
    }
}