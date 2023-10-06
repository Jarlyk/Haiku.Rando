using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Haiku.Rando.Topology;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Haiku.Rando.Checks
{
    /// <summary>
    /// This component handles all in-world checks as a 'shiny' pickup;
    /// it's designed to provide additional context to an existing PickupItem
    /// component.
    /// </summary>
    public sealed class UniversalPickup : MonoBehaviour
    {
        public IRandoItem check;
        public bool midAir;

        public static void InitHooks()
        {
            On.PickupItem.Start += PickupItem_Start;
            On.PickupItem.TriggerPickup += PickupItemOnTriggerPickup;
            IL.PianoManager.AddPoint += ChangePianoCondition;
        }

        private void Start()
        {
            //PickupItem might have already run Start, so we need to repeat the active-setting process
            OnStart(GetComponent<PickupItem>());
        }

        private void OnStart(PickupItem pickup)
        {
            if (check == null) return;

            bool alreadyGot = check.Obtained();
            gameObject.SetActive(!alreadyGot);

            if (midAir)
            {
                pickup.interactAnimator.enabled = false;
            }
        }

        private static void PickupItem_Start(On.PickupItem.orig_Start orig, PickupItem self)
        {
            var universalPickup = self.GetComponent<UniversalPickup>();
            if (!universalPickup)
            {
                orig(self);
                return;
            }

            //Need to call orig to wire up rewiredInput
            self.triggerPin = true;
            orig(self);
            
            universalPickup.OnStart(self);
        }

        private static void PickupItemOnTriggerPickup(On.PickupItem.orig_TriggerPickup orig, PickupItem self)
        {
            var universalPickup = self.GetComponent<UniversalPickup>();
            if (!universalPickup)
            {
                orig(self);
                return;
            }

            var check = universalPickup.check;
            if (check == null) return;

            check.Trigger(self);
            self.gameObject.SetActive(false);
        }

        private static void ChangePianoCondition(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(i => i.MatchBrfalse(out _));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Func<bool, PianoManager, bool>)((orig, pm) =>
            {
                var p = pm.reward.GetComponent<UniversalPickup>();
                return p == null ? orig : p.check.Obtained();
            }));
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!midAir) return;

            if (collision.CompareTag("Player"))
            {
                var pickup = GetComponent<PickupItem>();
                if (!pickup.collected)
                    pickup.TriggerPickup();
            }
        }

        internal static void ReplaceWrench(IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectOfType<PickupWrench>().gameObject;
            Replace(obj, replacement, false);
        }

        internal static void ReplaceBulblet(IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectOfType<PickupBulb>().gameObject;
            var newObj = Replace(obj, replacement, false);

            if (!GameManager.instance.bosses[2].defeated)
            {
                //Bulblet pickup gets activated upon boss death
                newObj.SetActive(false);
                BeeHiveItemReplacer.ReplaceCheck(replacement, newObj);
            }
        }

        internal static void ReplaceAbility(IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectOfType<UnlockTutorial>().gameObject;
            Replace(obj, replacement, true);
        }

        internal static void ReplaceItem(RandoCheck orig, IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(
                p => p.itemID == orig.CheckId && p.saveID == orig.SaveId)?.gameObject;
            if (orig.CheckId == (int)ItemId.CapsuleFragment)
            {
                var newObj = Replace(obj, replacement, false);
                if (orig.SceneId == SpecialScenes.Quatern)
                {
                    newObj.SetActive(false);
                    QuaternRewardReplacer.ReplaceCheck(orig, replacement, newObj);
                }
            }
            else if (orig.CheckId == (int)ItemId.Tape)
            {
                Attach(obj, replacement, false);
            }
            else
            {
                Attach(obj, replacement, false);
            }
        }

        internal static void ReplaceChip(RandoCheck orig, IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(
                p => p.triggerChip &&
                GameManager.instance.getChipNumber(p.chipIdentifier) == orig.CheckId)?.gameObject;
            Attach(obj, replacement, false);
            switch (orig.SceneId)
            {
                case 69:
                    LinkToCarBattery(obj);
                    break;
                case SpecialScenes.Quatern:
                    obj.SetActive(false);
                    QuaternRewardReplacer.ReplaceCheck(orig, replacement, obj);
                    break;
                case 95:
                    MotherRewardReplacer.ReplaceCheck(replacement);
                    break;
            }
        }

        internal static void ReplaceChipSlot(RandoCheck orig, IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(
                p => p.triggerChipSlot && p.chipSlotNumber == orig.CheckId)?.gameObject;
            Replace(obj, replacement, false);
        }

        internal static void ReplaceMapDisruptor(IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectOfType<Disruptor>().gameObject;
            Replace(obj, replacement, false);
        }

        internal static void ReplaceLore(RandoCheck orig, IRandoItem replacement)
        {
            var sentences = CheckManager.LoreTabletText[orig.CheckId];
            var oldObject = SceneUtils.FindObjectsOfType<DialogueTrigger>()
                .FirstOrDefault(t => t.dialogue.sentences.SequenceEqual(sentences))
                ?.gameObject;
            oldObject ??= SceneUtils.FindObjectsOfType<MultipleDialogueTrigger>()
                .FirstOrDefault(t => t.dialogueGroups.SelectMany(d => d.sentences).SequenceEqual(sentences))
                ?.gameObject;
            Replace(oldObject, replacement, false);
        }

        internal static void ReplaceLever(RandoCheck orig, IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectsOfType<SwitchDoor>().FirstOrDefault(
                p => p.doorID == orig.CheckId)?.gameObject;
            Replace(obj, replacement, false);
        }

        internal static void ReplacePowerCell(RandoCheck orig, IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectsOfType<PowerCell>().FirstOrDefault(
                p => p.saveID == orig.SaveId)?.gameObject;
            var r = Replace(obj, replacement, true);
            if (orig.SceneId == 98 && SceneUtils.FindObjectOfType<MischievousMechanic>() is MischievousMechanic mm)
            {
                r.SetActive(false);
                mm.reward = r;
            }
        }

        internal static void ReplaceCoolant(RandoCheck orig, IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(
                p => p.triggerCoolant && p.saveID == orig.SaveId)?.gameObject;
            Attach(obj, replacement, false);
        }

        internal static void ReplaceTrainStation(IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectOfType<TrainTicket>().gameObject;
            Replace(obj, replacement, false);
        }

        internal static void ReplaceMoneyPile(RandoCheck orig, IRandoItem replacement)
        {
            var obj = SceneUtils.FindObjectsOfType<SmallMoneyPile>().FirstOrDefault(
                p => p.pileID == orig.CheckId)?.gameObject;
            Replace(obj, replacement, true);
        }

        private static void Attach(GameObject obj, IRandoItem replacement, bool midAir)
        {
            var universalPickup = obj.AddComponent<UniversalPickup>();
            universalPickup.check = replacement;
            universalPickup.midAir = midAir;

            var pickup = obj.GetComponent<PickupItem>();
            pickup.saveID = replacement is RandoCheck rc ? rc.SaveId : 0;
        }

        //Special-case: Car Battery death object linkage
        private static void LinkToCarBattery(GameObject obj)
        {
            var carBattery = SceneUtils.FindObjectOfType<CarBattery>();
            carBattery.deathObject = obj;

            var rb = obj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            var collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = 0.1f;
            obj.layer = (int)LayerId.GroundCollision;
            //TODO: Go to Car Battery and find the actual settings for this
        }

        private static GameObject Replace(GameObject oldObject, IRandoItem replacement, bool midAir)
        {
            oldObject.SetActive(false);
            var oldPickup = oldObject.GetComponent<PickupItem>();
            if (oldPickup)
            {
                oldPickup.saveID = replacement is RandoCheck rc ? rc.SaveId : 0;
            }

            var newObject = UnityEngine.Object.Instantiate(HaikuResources.PrefabGenericPickup, oldObject.transform.position, oldObject.transform.rotation);

            Attach(newObject, replacement, midAir);
            return newObject;
        }
    }
}