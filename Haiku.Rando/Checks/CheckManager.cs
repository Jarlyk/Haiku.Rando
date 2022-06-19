using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Xml;
using Haiku.Rando.Logic;
using Haiku.Rando.Topology;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Haiku.Rando.Checks
{
    public sealed class CheckManager
    {
        public static readonly CheckManager Instance = new CheckManager();

        private const float PickupTextDuration = 4f;

        private RandoCheck _fireResReplacement;
        private RandoCheck _waterResReplacement;

        public CheckRandomizer Randomizer { get; set; }

        public static void InitHooks()
        {
            IL.e7FireWaterTrigger.Start += E7FireWaterTrigger_Start;
            On.e7UpgradeShop.TriggerUpgrade += E7UpgradeShop_TriggerUpgrade;

            //TODO: Bulblet check support for disabling if boss not defeated
        }

        public void OnSceneLoaded(int sceneId)
        {
            if (Randomizer == null)
            {
                //No randomizer loaded, so nothing to do
                return;
            }

            if (!Randomizer.Topology.Scenes.TryGetValue(sceneId, out var scene))
            {
                //This scene doesn't require randomization
                return;
            }

            foreach (var original in scene.Nodes.OfType<RandoCheck>())
            {
                if (Randomizer.CheckMapping.TryGetValue(original, out var replacement))
                {
                    ReplaceCheck(original, replacement);
                }
            }
        }

        public void ReplaceCheck(RandoCheck original, RandoCheck replacement)
        {
            if (original.IsShopItem)
            {
                ReplaceShopCheck(original, replacement);
                return;
            }

            //Fire/water checks are injected directly into the e7 shop flow
            if (original.Type == CheckType.FireRes)
            {
                _fireResReplacement = replacement;
                return;
            }

            if (original.Type == CheckType.WaterRes)
            {
                _waterResReplacement = replacement;
                return;
            }

            GameObject oldObject = null;
            bool midAir = false;
            bool canReuseObject = false;

            switch (original.Type)
            {
                case CheckType.Wrench:
                    oldObject = Object.FindObjectOfType<PickupWrench>().gameObject;
                    break;
                case CheckType.Bulblet:
                    oldObject = Object.FindObjectOfType<PickupBulb>().gameObject;
                    break;
                case CheckType.Ability:
                    oldObject = Object.FindObjectOfType<UnlockTutorial>().gameObject;
                    midAir = true;
                    break;
                case CheckType.Item:
                    oldObject = Object.FindObjectsOfType<PickupItem>().First(p => p.itemID == original.CheckId && p.saveID == original.SaveId).gameObject;
                    canReuseObject = true;
                    break;
                case CheckType.Chip:
                    oldObject = Object.FindObjectsOfType<PickupItem>().First(p => p.triggerChip && GameManager.instance.getChipNumber(p.chipIdentifier) == original.CheckId).gameObject;
                    canReuseObject = true;
                    break;
                case CheckType.ChipSlot:
                    oldObject = Object.FindObjectsOfType<PickupItem>().First(p => p.triggerChipSlot && p.chipSlotNumber == original.CheckId).gameObject;
                    canReuseObject = true;
                    break;
                case CheckType.MapDisruptor:
                    oldObject = Object.FindObjectOfType<Disruptor>().gameObject;
                    break;
                case CheckType.Lore:
                    //TODO
                    break;
                case CheckType.Lever:
                    oldObject = Object.FindObjectsOfType<SwitchDoor>().First(p => p.doorID == original.CheckId).gameObject;
                    break;
                case CheckType.PartsMonument:
                    //TODO
                    break;
                case CheckType.PowerCell:
                    oldObject = Object.FindObjectsOfType<PowerCell>().First(p => p.saveID == original.SaveId).gameObject;
                    midAir = true;
                    break;
                case CheckType.Coolant:
                    oldObject = Object.FindObjectsOfType<PickupItem>().First(p => p.triggerCoolant && p.saveID == original.SaveId).gameObject;
                    canReuseObject = true;
                    break;
                case CheckType.FireRes:
                case CheckType.WaterRes:
                    //These shouldn't be reached, as they're handled earlier
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!oldObject)
            {
                Debug.Log($"Check type {original.Type} not yet supported");
                return;
            }

            GameObject newObject = null;
            if (canReuseObject)
            {
                newObject = oldObject;
            }
            else
            {
                oldObject.SetActive(false);

                //TODO: Get prefab for shiny pickup
                GameObject pickupPrefab = null;
                newObject = Object.Instantiate(pickupPrefab, oldObject.transform.position, oldObject.transform.rotation);
            }

            if (newObject)
            {
                var universalPickup = newObject.AddComponent<UniversalPickup>();
                universalPickup.check = replacement;
                universalPickup.midAir = midAir;
            }
        }

        private void ReplaceShopCheck(RandoCheck original, RandoCheck replacement)
        {
            //TODO
        }

        private static void E7FireWaterTrigger_Start(ILContext il)
        {
            var c = new ILCursor(il);

            //Position after reading fireWater and just before the jump
            c.GotoNext(i => i.MatchLdarg(0),
                       i => i.MatchLdfld(typeof(e7FireWaterTrigger), "fireWater"),
                       i => i.MatchBrfalse(out _));
            c.Index += 2;

            //We want to keep this bool for the existing Brfalse check, but first we're going to use it for our own check
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, e7FireWaterTrigger, bool>>(HandleFireWaterCheck);
            var end = c.DefineLabel();
            c.Emit(OpCodes.Brtrue, end);

            c.GotoNext(i => i.MatchRet());
            c.MarkLabel(end);
        }

        private static bool HandleFireWaterCheck(bool fireWater, e7FireWaterTrigger self)
        {
            RandoCheck check = fireWater ? Instance._fireResReplacement : Instance._waterResReplacement;
            if (check == null) return false;

            if (AlreadyGotCheck(check))
            {
                self.anim.SetTrigger("powerOn");
                self.triggered = true;
            }

            return true;
        }

        private static void E7UpgradeShop_TriggerUpgrade(On.e7UpgradeShop.orig_TriggerUpgrade orig, e7UpgradeShop self, bool fireWater)
        {
            var check = fireWater ? Instance._fireResReplacement : Instance._waterResReplacement;

            if (check != null)
            {
                if (AlreadyGotCheck(check)) return;
                TriggerCheck(self, check);
            }
            else
            {
                orig(self, fireWater);
            }
        }

        public static bool AlreadyGotCheck(RandoCheck check)
        {
            bool alreadyGot = false;
            switch (check.Type)
            {
                case CheckType.Wrench:
                    alreadyGot = GameManager.instance.canHeal;
                    break;
                case CheckType.Bulblet:
                    alreadyGot = GameManager.instance.lightBulb;
                    break;
                case CheckType.Ability:
                    alreadyGot = HasAbility((AbilityId)check.CheckId);
                    break;
                case CheckType.Item:
                case CheckType.Chip:
                case CheckType.ChipSlot:
                case CheckType.Coolant:
                    alreadyGot = GameManager.instance.worldObjects[check.SaveId].collected;
                    break;
                case CheckType.MapDisruptor:
                    alreadyGot = GameManager.instance.disruptors[check.CheckId].destroyed;
                    break;
                case CheckType.Lore:
                    //TODO
                    break;
                case CheckType.Lever:
                    alreadyGot = GameManager.instance.doors[check.CheckId].opened;
                    break;
                case CheckType.PartsMonument:
                    //TODO
                    break;
                case CheckType.PowerCell:
                    alreadyGot = GameManager.instance.powerCells[check.CheckId].collected;
                    break;
                case CheckType.FireRes:
                    alreadyGot = GameManager.instance.fireRes;
                    break;
                case CheckType.WaterRes:
                    alreadyGot = GameManager.instance.waterRes;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return alreadyGot;
        }

        public static void TriggerCheck(MonoBehaviour self, RandoCheck check)
        {
            var refPickup = HaikuResources.RefPickupItem;
            bool hasWorldObject = true;
            switch (check.Type)
            {
                case CheckType.Wrench:
                    GameManager.instance.canHeal = true;
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_HEALING_WRENCH_TITLE", "", PickupTextDuration);
                    break;
                case CheckType.Bulblet:
                    GameManager.instance.lightBulb = true;
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_LIGHT_BULB_TITLE", "", PickupTextDuration);
                    hasWorldObject = false;
                    break;
                case CheckType.Ability:
                    GiveAbility((AbilityId)check.CheckId);
                    var refUnlock = HaikuResources.RefUnlockTutorial;
                    var ability = refUnlock.abilities[check.CheckId];
                    CameraBehavior.instance.ShowLeftCornerUI(ability.image, ability.title, "", PickupTextDuration);
                    hasWorldObject = false;
                    break;
                case CheckType.Item:
                    InventoryManager.instance.AddItem(check.CheckId);
                    CameraBehavior.instance.ShowLeftCornerUI(InventoryManager.instance.items[check.CheckId].image,
                                                             InventoryManager.instance.items[check.CheckId].itemName,
                                                             "",
                                                             PickupTextDuration);
                    break;
                case CheckType.Chip:
                    GameManager.instance.chip[check.CheckId].collected = true;
                    CameraBehavior.instance.ShowLeftCornerUI(GameManager.instance.chip[check.CheckId].image,
                                                             GameManager.instance.chip[check.CheckId].title,
                                                             "",
                                                             PickupTextDuration);
                    AchievementManager.instance.CheckNumbersOfChipsCollected();
                    break;
                case CheckType.ChipSlot:
                    GameManager.instance.chipSlot[check.CheckId].collected = true;
                    CameraBehavior.instance.ShowLeftCornerUI(refPickup.chipSlotImage, refPickup.chipSlotTitle, "", PickupTextDuration);
                    break;
                case CheckType.MapDisruptor:
                    GameManager.instance.disruptors[check.CheckId].destroyed = true;
                    CameraBehavior.instance.Shake(0.2f, 0.2f);
                    var refDisruptor = HaikuResources.RefDisruptor;
                    CameraBehavior.instance.ShowLeftCornerUI(refDisruptor.mapImage, refDisruptor.disruptorDestroyedText, "", PickupTextDuration);
                    AchievementManager.instance.CheckNumberOfDisruptorsDestroyed();
                    break;
                case CheckType.Lore:
                    //TODO
                    break;
                case CheckType.Lever:
                    //TODO
                    break;
                case CheckType.PartsMonument:
                    //TODO
                    break;
                case CheckType.PowerCell:
                    self.StartCoroutine(RemoveHeat());
                    GameManager.instance.powerCells[check.CheckId].collected = true;
                    //TODO: Sound effect and particles from PowerCell?
                    AchievementManager.instance.CheckNumbersOfPowercellsCollected();
                    break;
                case CheckType.Coolant:
                    GameManager.instance.coolingPoints++;
                    CameraBehavior.instance.ShowLeftCornerUI(refPickup.coolantImage, refPickup.coolantTitle, "", PickupTextDuration);
                    break;
                case CheckType.FireRes:
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_FIRE_RES_TITLE", "_FIRE_RES_DESCRIPTION", 4f);
                    GameManager.instance.fireRes = true;
                    hasWorldObject = false;
                    break;
                case CheckType.WaterRes:
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_WATER_RES_TITLE", "_WATER_RES_DESCRIPTION", 4f);
                    GameManager.instance.waterRes = true;
                    hasWorldObject = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (hasWorldObject)
            {
                GameManager.instance.worldObjects[check.SaveId].collected = true;
            }

            SoundManager.instance.PlayOneShot(refPickup.pickupSFXPath);
        }

        public static IEnumerator RemoveHeat()
        {
            yield return new WaitForSeconds(0.5f);
            ManaManager.instance.RemoveHeat(270f);
        }

        private static void GiveAbility(AbilityId abilityId)
        {
            switch (abilityId)
            {
                case AbilityId.Magnet:
                    GameManager.instance.canWallJump = true;
                    break;
                case AbilityId.Ball:
                    GameManager.instance.canRoll = true;
                    break;
                case AbilityId.Bomb:
                    GameManager.instance.canBomb = true;
                    break;
                case AbilityId.Blink:
                    GameManager.instance.canTeleport = true;
                    break;
                case AbilityId.DoubleJump:
                    GameManager.instance.canDoubleJump = true;
                    break;
                case AbilityId.Grapple:
                    GameManager.instance.canGrapple = true;
                    break;
            }
        }

        public static bool HasAbility(AbilityId abilityId)
        {
            switch (abilityId)
            {
                case AbilityId.Magnet:
                    return GameManager.instance.canWallJump;
                case AbilityId.Ball:
                    return GameManager.instance.canRoll;
                case AbilityId.Bomb:
                    return GameManager.instance.canBomb;
                case AbilityId.Blink:
                    return GameManager.instance.canTeleport;
                case AbilityId.DoubleJump:
                    return GameManager.instance.canDoubleJump;
                case AbilityId.Grapple:
                    return GameManager.instance.canGrapple;
                default:
                    return false;
            }
        }
    }
}
