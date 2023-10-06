using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class SealantShopItemReplacer : MonoBehaviour
    {
        private IRandoItem fireResReplacement;
        private IRandoItem waterResReplacement;

        private IRandoItem Check(bool fireWater) =>
            fireWater ? fireResReplacement : waterResReplacement;

        public static void InitHooks()
        {
            IL.e7FireWaterTrigger.Start += ReplaceAnimationCheck;
            On.e7UpgradeShop.TriggerUpgrade += TriggerCheck;
        }

        private static void ReplaceAnimationCheck(ILContext il)
        {
            var c = new ILCursor(il);

            //Position after reading fireWater and just before the jump
            c.GotoNext(i => i.MatchLdarg(0),
                       i => i.MatchLdfld(typeof(e7FireWaterTrigger), "fireWater"),
                       i => i.MatchBrfalse(out _));
            c.Index += 2;

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, e7FireWaterTrigger, bool>>(VerifyCheckReplaced);
            var end = c.DefineLabel();
            c.Emit(OpCodes.Brtrue, end);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldfld, typeof(e7FireWaterTrigger).GetField("fireWater", BindingFlags.Instance | BindingFlags.NonPublic));

            c.GotoNext(i => i.MatchRet());
            c.MarkLabel(end);
        }

        private static bool VerifyCheckReplaced(bool fireWater, e7FireWaterTrigger self)
        {
            var replacer = self.GetComponent<SealantShopItemReplacer>();
            if (replacer == null)
            {
                return false;
            }
            var check = replacer.Check(fireWater);
            if (check == null)
            {
                return false;
            }
            if (check.Obtained())
            {
                self.anim.SetTrigger("powerOn");
                self.triggered = true;
            }
            return true;
        }

        private static void TriggerCheck(On.e7UpgradeShop.orig_TriggerUpgrade orig, e7UpgradeShop self, bool fireWater)
        {
            var replacer = self.GetComponent<SealantShopItemReplacer>();
            if (replacer == null)
            {
                orig(self, fireWater);
                return;
            }
            var check = replacer.Check(fireWater);
            if (check == null)
            {
                orig(self, fireWater);
            }
            else if (!check.Obtained())
            {
                check.Trigger(self);
            }
        }

        private static void ReplaceCheck(bool fireWater, IRandoItem replacement)
        {
            var trigger = SceneUtils.FindObjectsOfType<e7FireWaterTrigger>().First(t => t.fireWater == fireWater);
            var shop = SceneUtils.FindObjectOfType<e7UpgradeShop>();
            if (trigger == null || shop == null)
            {
                throw new InvalidOperationException("attempted to replace fire sealant check without the shop being present");
            }
            trigger.dialogue.sentence = replacement.UIDef().Name;
            var rt = trigger.gameObject.AddComponent<SealantShopItemReplacer>();
            if (!shop.gameObject.TryGetComponent<SealantShopItemReplacer>(out var rs))
            {
                rs = shop.gameObject.AddComponent<SealantShopItemReplacer>();
            }
            if (fireWater)
            {
                rt.fireResReplacement = replacement;
                rs.fireResReplacement = replacement;
            }
            else
            {
                rt.waterResReplacement = replacement;
                rs.waterResReplacement = replacement;
            }
        }

        public static void ReplaceFire(IRandoItem replacement)
        {
            ReplaceCheck(true, replacement);
        }

        public static void ReplaceWater(IRandoItem replacement)
        {
            ReplaceCheck(false, replacement);
        }
    }
}