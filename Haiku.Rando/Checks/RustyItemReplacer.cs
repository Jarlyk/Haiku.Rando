using System;
using System.Linq;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class RustyItemReplacer : MonoBehaviour
    {
        private IRandoItem replacement;

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
                replacer.replacement.Trigger(self);
            }
        }

        public static void ReplaceCheck(RustyType origType, IRandoItem replacement)
        {
            foreach (var rusty in SceneUtils.FindObjectsOfType<Rusty>())
            {
                if (Matches(origType, rusty))
                {
                    var replacer = rusty.gameObject.AddComponent<RustyItemReplacer>();
                    replacer.replacement = replacement;
                    // Rusty usually uses having talked to them as the trigger to disappear.
                    // Instead, the trigger should be having picked up the check.
                    // The Rusty near the hand monument is also coded to disappear if any of the
                    // other Rusties are visited. This is not what we want when we have
                    // them randomized.
                    foreach (var disabler in rusty.GetComponents<EnableIfNPC>())
                    {
                        GameObject.Destroy(disabler);
                    }
                    // In addition to the above, in Water Ducts and Forgotten Ruins, there are actually
                    // two Rusties in the scene, and one of them vanishes depending on whether or not
                    // you have the Character marker. This makes sense in vanilla, but not rando.
                    foreach (var switcher in SceneUtils.FindObjectsOfType<RustyBankVendor>())
                    {
                        GameObject.Destroy(switcher);
                    }
                    rusty.gameObject.SetActive(!replacement.Obtained());
                    
                    if (origType == RustyType.Health)
                    {
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
                else
                {
                    // Hide any other alternative Rusties in the same scene.
                    // (happens in Water Ducts, Forgotten Ruins and Mainframe)
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