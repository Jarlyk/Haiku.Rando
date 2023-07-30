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
            // MMHook's predefined hook for SwitchDoor.TakeDamage is broken,
            // likely because of Vector2 being a struct.
            new MMDetour.Hook(
                typeof(SwitchDoor).GetMethod("TakeDamage", BindingFlags.Public | BindingFlags.Instance),
                ReplaceItem
            );

            On.IncineratorBridgeSwitch.Start += ChangeIncineratorActivationCondition;
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

        private static void ChangeIncineratorActivationCondition(
            On.IncineratorBridgeSwitch.orig_Start orig, IncineratorBridgeSwitch self)
        {
            // Unlike SwitchDoor, this one does not raise the bridge in Start.
            // Still, let the original code run to fill in switchAnim and switchCollider
            // and potentially run other mods' hooks.
            orig(self);

            var lr = self.GetComponent<LeverReplacer>();
            if (lr != null)
            {
                var gotCheck = CheckManager.AlreadyGotCheck(lr.replacement);
                self.switchAnim.SetBool("open", gotCheck);
                self.switchCollider.enabled = !gotCheck;
            }
        }

        private static void ReplaceItem(
            Action<SwitchDoor, int, int, Vector2> orig,
            SwitchDoor self, int a, int b, Vector2 playerPos)
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

        private static void ReplaceIncineratorItem(
            Action<IncineratorBridgeSwitch, int, int, Vector2> orig,
            IncineratorBridgeSwitch self, int a, int b, Vector2 playerPos)
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

        private static GameObject FindLever(int doorID)
        {
            var lever = SceneUtils.FindObjectsOfType<SwitchDoor>()
                .Where(s => s.doorID == doorID)
                .FirstOrDefault();
            if (lever != null)
            {
                return lever.gameObject;
            }
            return SceneUtils.FindObjectsOfType<IncineratorBridgeSwitch>()
                .Where(s => s.doorID == doorID)
                .FirstOrDefault()?.gameObject;
        }

        public static void ReplaceCheck(RandoCheck orig, RandoCheck replacement)
        {
            var lev = FindLever(orig.CheckId);
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