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
        private RandoCheck fireResReplacement;
        private RandoCheck waterResReplacement;

        private RandoCheck Check(bool fireWater) =>
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
            if (CheckManager.AlreadyGotCheck(check))
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
            else if (!CheckManager.AlreadyGotCheck(check))
            {
                CheckManager.TriggerCheck(self, check);
            }
        }

        private static void ReplaceCheck(bool fireWater, RandoCheck replacement)
        {
            var trigger = SceneUtils.FindObjectsOfType<e7FireWaterTrigger>().First(t => t.fireWater == fireWater);
            var shop = SceneUtils.FindObjectOfType<e7UpgradeShop>();
            if (trigger == null || shop == null)
            {
                throw new InvalidOperationException("attempted to replace fire sealant check without the shop being present");
            }
            trigger.dialogue.sentence = UIDef.Of(replacement).Name;
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

        public static void ReplaceFire(RandoCheck replacement)
        {
            ReplaceCheck(true, replacement);
        }

        public static void ReplaceWater(RandoCheck replacement)
        {
            ReplaceCheck(false, replacement);
        }
    }
}