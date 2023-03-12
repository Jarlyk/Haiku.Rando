using System;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class MotherRewardReplacer : MonoBehaviour
    {
        private RandoCheck replacement;

        public static void InitHooks()
        {
            IL.MotherWindUp.Start += ChangeActivationCondition;
            IL.MotherWindUp.EndDialogueAction += ChangeActivationCondition;
        }

        private static void ChangeActivationCondition(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(i => i.MatchBrfalse(out _));
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Func<MotherWindUp, bool>)IsReplacementCollected);
        }

        private static bool IsReplacementCollected(MotherWindUp mother)
        {
            var replacer = mother.GetComponent<MotherRewardReplacer>();
            if (replacer == null)
            {
                //Check wasn't replaced, so use original logic
                return GameManager.instance.chip[GameManager.instance.getChipNumber("b_FastHeal")].collected;
            }
            return CheckManager.AlreadyGotCheck(replacer.replacement);
        }

        public static void ReplaceCheck(RandoCheck replacement)
        {
            var mother = SceneUtils.FindObjectOfType<MotherWindUp>();
            if (mother == null)
            {
                throw new InvalidOperationException("attempted to replace Mother check while she is not present");
            }
            var replacer = mother.gameObject.AddComponent<MotherRewardReplacer>();
            replacer.replacement = replacement;
        }
    }
}