using System;
using System.Reflection;
using System.Linq;
using MMDetour = MonoMod.RuntimeDetour;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class LeverReplacer : MonoBehaviour
    {
        public RandoCheck replacement;

        public static void InitHooks()
        {
            On.SwitchDoor.Start += ChangeActivationCondition;
            new MMDetour.Hook(
                typeof(SwitchDoor).GetMethod("TakeDamage", BindingFlags.Public | BindingFlags.Instance),
                ReplaceItem
            );
        }

        private static void ChangeActivationCondition(On.SwitchDoor.orig_Start orig, SwitchDoor self)
        {
            // Let the door open if it has been unlocked
            // whether by a vanilla lever, or by a corresponding rando
            // lever pickup.
            orig(self);
            
            var lr = self.GetComponent<LeverReplacer>();
            if (lr != null)
            {
                var gotCheck = CheckManager.AlreadyGotCheck(lr.replacement);
                self.switchAnim.SetBool("open", gotCheck);
                self.switchCollider.enabled = !gotCheck;
            }
        }

        private static void ReplaceItem(Action<SwitchDoor, int, int, Vector2> orig, SwitchDoor self, int a, int b, Vector2 playerPos)
        {
            var lr = self.GetComponent<LeverReplacer>();
            if (lr == null)
            {
                orig(self, a, b, playerPos);
                return;
            }
            // same effects as in the vanilla code
            SoundManager.instance.PlayOneShotOnTarget(self.switchSFXPath, self.transform.position);
            CameraBehavior.instance.Shake(.2f, .1f);
            self.switchAnim.SetBool("open", true);
            self.switchCollider.enabled = false;
            // give the item
            CheckManager.TriggerCheck(self, lr.replacement);
        }

        public static void ReplaceCheck(RandoCheck orig, RandoCheck replacement)
        {
            var lev = SceneUtils.FindObjectsOfType<SwitchDoor>()
                .Where(s => s.doorID == orig.CheckId)
                .FirstOrDefault();
            if (lev == null)
            {
                throw new InvalidOperationException($"attempted to replace lever {orig.CheckId} that is not present");
            }
            var lr = lev.gameObject.AddComponent<LeverReplacer>();
            lr.replacement = replacement;
            lr.enabled = true;
        }
    }
}