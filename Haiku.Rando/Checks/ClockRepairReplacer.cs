using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class ClockRepairReplacer : MonoBehaviour
    {
        public static void InitHooks()
        {
            On.FixClockAndTrain.RepairAction += GiveItem;
            On.FixClockAndTrain.Start += EnableIfNotGiven;
            IL.FixClockAndTrain.Update += EnableIfNotGivenPart2;
        }

        public RandoCheck replacement;

        private static void EnableIfNotGiven(On.FixClockAndTrain.orig_Start orig, FixClockAndTrain self)
        {
            orig(self);
            var r = self.GetComponent<ClockRepairReplacer>();
            if (r != null)
            {
                var unclaimed = !CheckManager.AlreadyGotCheck(r.replacement);
                self.gameObject.SetActive(unclaimed);
                if (unclaimed && self.rewiredInput == null)
                {
                    self.rewiredInput = Rewired.ReInput.players.GetPlayer(0);
                }
            }
        }

        private static void EnableIfNotGivenPart2(ILContext il)
        {
            var c = new ILCursor(il);

            while (c.TryGotoNext(MoveType.After, i => i.MatchLdfld(typeof(GameManager), "trainUnlocked")))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((Func<bool, FixClockAndTrain, bool>)IsCheckCollected);
            }
        }

        private static bool IsCheckCollected(bool orig, FixClockAndTrain obj)
        {
            var r = obj.GetComponent<ClockRepairReplacer>();
            return r == null ? orig : CheckManager.AlreadyGotCheck(r.replacement);
        }

        private static void GiveItem(On.FixClockAndTrain.orig_RepairAction orig, FixClockAndTrain self)
        {
            var r = self.GetComponent<ClockRepairReplacer>();
            if (r == null)
            {
                orig(self);
            }
            else
            {
                CheckManager.TriggerCheck(self, r.replacement);
                self.gameObject.SetActive(false);
            }
        }

        public static void ReplaceCheck(RandoCheck replacement)
        {
            var f = SceneUtils.FindObjectOfType<FixClockAndTrain>();
            if (f == null)
            {
                throw new InvalidOperationException("attempted to replace Clock check while repair device is not present");
            }
            var r = f.gameObject.AddComponent<ClockRepairReplacer>();
            r.replacement = replacement;
            f.dialogue.sentence = UIDef.Of(replacement).Name;
        }
    }
}