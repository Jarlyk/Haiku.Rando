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
            new MMDetour.Hook(
                typeof(IncineratorBridgeSwitch).GetMethod("TakeDamage", BindingFlags.Public | BindingFlags.Instance),
                ReplaceIncineratorItem
            );

            On.PistonDoor.Start += ChangePistonSwitchActivationCondition;
            new MMDetour.Hook(
                typeof(PistonDoorSwitch).GetMethod("TakeDamage", BindingFlags.Public | BindingFlags.Instance),
                ReplacePistonItem
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

        private static void ChangePistonSwitchActivationCondition(
            On.PistonDoor.orig_Start orig, PistonDoor self)
        {
            // Let the bridge come up if it has been unlocked
            // whether by the vanilla lever, or by a corresponding rando
            // lever pickup.
            orig(self);

            var lr = self.GetComponent<LeverReplacer>();
            if (lr != null)
            {
                var gotCheck = CheckManager.AlreadyGotCheck(lr.replacement);
                self.switchAnim.SetBool("open", !gotCheck);
                self.switchColl.enabled = gotCheck;
                // This switch is normally hidden when the Pinion's Expanse
                // entrance has not been crossed yet; it's very easy to reach
                // it through other means in a rando, though.
                self.switchObject.SetActive(true);
            }
        }

        private static void ReplacePistonItem(
            Action<PistonDoorSwitch, int, int, Vector2> orig,
            PistonDoorSwitch self, int a, int b, Vector2 playerPos)
        {
            var lr = self.GetComponent<LeverReplacer>();
            if (lr != null)
            {
                CheckManager.TriggerCheck(self, lr.replacement);
            }
            else
            {
                orig(self, a, b, playerPos);
            }
        }

        private static MonoBehaviour FindLever(int doorID)
        {
            var lever = SceneUtils.FindObjectsOfType<SwitchDoor>()
                .Where(s => s.doorID == doorID)
                .FirstOrDefault();
            if (lever != null)
            {
                return lever;
            }
            var bridge = SceneUtils.FindObjectsOfType<IncineratorBridgeSwitch>()
                .Where(s => s.doorID == doorID)
                .FirstOrDefault();
            if (bridge != null)
            {
                return bridge;
            }
            return SceneUtils.FindObjectsOfType<PistonDoorSwitch>()
                .Where(s => s.pistonDoorScript.doorID == doorID)
                .FirstOrDefault();
        }

        private const int OldArcadiaElevatorLever = 71;

        public static void ReplaceCheck(RandoCheck orig, RandoCheck replacement)
        {
            if (orig.CheckId == OldArcadiaElevatorLever)
            {
                ReplaceOldArcadiaElevatorCheck(replacement);
                return;
            }
            var lev = FindLever(orig.CheckId);
            if (lev == null)
            {
                throw new InvalidOperationException($"attempted to replace lever {orig.CheckId} that is not present");
            }
            var lr = lev.gameObject.AddComponent<LeverReplacer>();
            lr.replacement = replacement;
            lr.enabled = true;
            if (lev is PistonDoorSwitch pds)
            {
                var lr2 = pds.pistonDoorScript.gameObject.AddComponent<LeverReplacer>();
                lr2.replacement = replacement;
                lr2.enabled = true;
            }
        }

        private static void ReplaceOldArcadiaElevatorCheck(RandoCheck replacement)
        {
            // The Old Arcadia elevator lever is actually two levers, one for each door
            // on each side of the elevator. We attach the check to one lever and disable
            // the other.
            var levers = SceneUtils.FindObjectsOfType<SwitchDoor>()
                .Where(s => s.doorID == OldArcadiaElevatorLever)
                .ToList();
            if (levers.Count == 0)
            {
                throw new InvalidOperationException($"attempted to replace lever {OldArcadiaElevatorLever} that is not present");
            }
            var lr = levers[0].gameObject.AddComponent<LeverReplacer>();
            lr.replacement = replacement;
            lr.enabled = true;
            for (var i = 1; i < levers.Count; i++)
            {
                GameObject.Destroy(levers[i].switchCollider.gameObject);
            }
        }
    }
}