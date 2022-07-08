using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Reflection;
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
            On.BeeHive.TriggerBulbItem += BeeHive_TriggerBulbItem;
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
                    ReplaceCheck(sceneId, original, replacement);
                }
            }
        }

        public void ReplaceCheck(int sceneId, RandoCheck original, RandoCheck replacement)
        {
            Debug.Log($"Replacing check {original} with {replacement}");
            if (original.IsShopItem)
            {
                ReplaceShopCheck(original, replacement);
                return;
            }

            //Fire/water checks are injected directly into the e7 shop flow
            if (original.Type == CheckType.FireRes)
            {
                _fireResReplacement = replacement;
                var trigger = SceneUtils.FindObjectsOfType<e7FireWaterTrigger>().First(t => t.fireWater);
                trigger.dialogue.sentence = GetSpoilerText(replacement);
                return;
            }

            if (original.Type == CheckType.WaterRes)
            {
                _waterResReplacement = replacement;
                var trigger = SceneUtils.FindObjectsOfType<e7FireWaterTrigger>().First(t => !t.fireWater);
                trigger.dialogue.sentence = GetSpoilerText(replacement);
                return;
            }

            GameObject oldObject = null;
            bool midAir = false;
            bool reuseObject = false;

            switch (original.Type)
            {
                case CheckType.Wrench:
                    oldObject = SceneUtils.FindObjectOfType<PickupWrench>().gameObject;
                    break;
                case CheckType.Bulblet:
                    oldObject = SceneUtils.FindObjectOfType<PickupBulb>().gameObject;
                    break;
                case CheckType.Ability:
                    oldObject = SceneUtils.FindObjectOfType<UnlockTutorial>().gameObject;
                    midAir = true;
                    break;
                case CheckType.Item:
                    oldObject = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(p => p.itemID == original.CheckId && p.saveID == original.SaveId)?.gameObject;
                    reuseObject = original.CheckId != (int)ItemId.CapsuleFragment;
                    break;
                case CheckType.Chip:
                    oldObject = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(p => p.triggerChip && GameManager.instance.getChipNumber(p.chipIdentifier) == original.CheckId)?.gameObject;
                    reuseObject = true;
                    break;
                case CheckType.ChipSlot:
                    oldObject = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(p => p.triggerChipSlot && p.chipSlotNumber == original.CheckId)?.gameObject;
                    reuseObject = false;
                    break;
                case CheckType.MapDisruptor:
                    oldObject = SceneUtils.FindObjectOfType<Disruptor>().gameObject;
                    break;
                case CheckType.Lore:
                    //TODO
                    break;
                case CheckType.Lever:
                    oldObject = SceneUtils.FindObjectsOfType<SwitchDoor>().FirstOrDefault(p => p.doorID == original.CheckId)?.gameObject;
                    break;
                case CheckType.PartsMonument:
                    //TODO
                    break;
                case CheckType.PowerCell:
                    oldObject = SceneUtils.FindObjectsOfType<PowerCell>().FirstOrDefault(p => p.saveID == original.SaveId)?.gameObject;
                    midAir = true;
                    break;
                case CheckType.Coolant:
                    oldObject = SceneUtils.FindObjectsOfType<PickupItem>().FirstOrDefault(p => p.triggerCoolant && p.saveID == original.SaveId)?.gameObject;
                    reuseObject = true;
                    break;
                case CheckType.TrainStation:
                    oldObject = SceneUtils.FindObjectOfType<TrainTicket>().gameObject;
                    //TODO
                    break;
                case CheckType.Clock:
                    //This is never randomized, but is important to logic
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
                Debug.Log($"Failed to find original object for check {original} in order to replace it");
                var pickups = SceneUtils.FindObjectsOfType<PickupItem>();
                Debug.Log($"Scene currently has {pickups.Length} PickupItem instances");
                return;
            }

            GameObject newObject = null;
            if (reuseObject)
            {
                newObject = oldObject;
            }
            else
            {
                oldObject.SetActive(false);
                newObject = Object.Instantiate(HaikuResources.PrefabGenericPickup, oldObject.transform.position, oldObject.transform.rotation);

                if (original.Type == CheckType.Bulblet && !GameManager.instance.bosses[2].defeated)
                {
                    //Bulblet pickup gets activated upon boss death
                    newObject.SetActive(false);
                }
            }

            if (newObject)
            {
                var universalPickup = newObject.AddComponent<UniversalPickup>();
                universalPickup.check = replacement;
                universalPickup.midAir = midAir;

                //Special-case: Car Battery death object linkage
                if (sceneId == 69 && original.Type == CheckType.Chip)
                {
                    var carBattery = SceneUtils.FindObjectOfType<CarBattery>();
                    carBattery.deathObject = newObject;

                    var rb = newObject.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0;
                    var collider = newObject.AddComponent<CircleCollider2D>();
                    collider.radius = 0.1f;
                    newObject.layer = (int)LayerId.GroundCollision;
                    //TODO: Go to Car Battery and find the actual settings for this
                }
            }
        }

        private void ReplaceShopCheck(RandoCheck original, RandoCheck replacement)
        {
            var button = SceneUtils.FindObjectsOfType<ShopItemButton>().FirstOrDefault(b => MatchesShop(b, original));
            if (button)
            {
                var replacer = button.gameObject.AddComponent<ShopItemReplacer>();
                replacer.check = replacement;
            }
            else
            {
                Debug.LogWarning($"Failed to find shop button matching original check {original}");
            }
        }

        private static bool MatchesShop(ShopItemButton button, RandoCheck check)
        {
            if (!check.IsShopItem) return false;

            switch (check.Type)
            {
                case CheckType.Item:
                    return button.item && button.itemID == check.CheckId;
                case CheckType.Chip:
                    return button.chip && GameManager.instance.getChipNumber(button.chipIdentifier) == check.CheckId;
                case CheckType.ChipSlot:
                    return button.chipSlot && button.chipSlotID == check.CheckId;
                case CheckType.PowerCell:
                    return button.powercell;
                default:
                    //Other types of checks will never show up in a standard item shop
                    return false;
            }
        }

        private static void E7FireWaterTrigger_Start(ILContext il)
        {
            var c = new ILCursor(il);

            //Position after reading fireWater and just before the jump
            c.GotoNext(i => i.MatchLdarg(0),
                       i => i.MatchLdfld(typeof(e7FireWaterTrigger), "fireWater"),
                       i => i.MatchBrfalse(out _));
            c.Index += 2;

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, e7FireWaterTrigger, bool>>(HandleFireWaterCheck);
            var end = c.DefineLabel();
            c.Emit(OpCodes.Brtrue, end);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldfld, typeof(e7FireWaterTrigger).GetField("fireWater", BindingFlags.Instance | BindingFlags.NonPublic));

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

        private static void BeeHive_TriggerBulbItem(On.BeeHive.orig_TriggerBulbItem orig, BeeHive self)
        {
            if (Instance.Randomizer == null)
            {
                orig(self);
                return;
            }

            var bulbCheck = Instance.Randomizer.CheckMapping.Keys.FirstOrDefault(c => c.Type == CheckType.Bulblet);
            if (bulbCheck != null)
            {
                //Instead of activating the original pickup object, we want to activate the new one instead
                var newCheck = Instance.Randomizer.CheckMapping[bulbCheck];
                self.bulbPickup = SceneUtils.FindObjectsOfType<UniversalPickup>()
                                            .First(p => p.check == newCheck).gameObject;
            }

            orig(self);
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
                case CheckType.TrainStation:
                    alreadyGot = GameManager.instance.trainStations[check.CheckId].unlockedStation;
                    break;
                case CheckType.Clock:
                    //This is never randomized, but is important to logic
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
                    CameraBehavior.instance.ShowLeftCornerUI(InventoryManager.instance.items[(int)ItemId.Wrench].image, "_HEALING_WRENCH_TITLE", "", PickupTextDuration);
                    break;
                case CheckType.Bulblet:
                    GameManager.instance.lightBulb = true;
                    CameraBehavior.instance.ShowLeftCornerUI(HaikuResources.ItemDesc().lightBulb.image.sprite, "_LIGHT_BULB_TITLE", "", PickupTextDuration);
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
                    var refChipSlot = HaikuResources.GetRefChipSlot(check.CheckId);
                    CameraBehavior.instance.ShowLeftCornerUI(refChipSlot.chipSlotImage, refChipSlot.chipSlotTitle, "", PickupTextDuration);
                    break;
                case CheckType.MapDisruptor:
                    GameManager.instance.disruptors[check.CheckId].destroyed = true;
                    CameraBehavior.instance.Shake(0.2f, 0.2f);
                    var refDisruptor = HaikuResources.RefDisruptor;
                    CameraBehavior.instance.ShowLeftCornerUI(refDisruptor.GetComponentInChildren<SpriteRenderer>(true).sprite, 
                                                             "_DISRUPTOR", 
                                                             "", 
                                                             PickupTextDuration);
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
                    CameraBehavior.instance.ShowLeftCornerUI(HaikuResources.RefPowerCell.GetComponentInChildren<SpriteRenderer>(true).sprite, 
                                                             "_POWERCELL",
                                                             "",
                                                             PickupTextDuration);
                    //TODO: Sound effect and particles from PowerCell?
                    AchievementManager.instance.CheckNumbersOfPowercellsCollected();
                    break;
                case CheckType.Coolant:
                    GameManager.instance.coolingPoints++;
                    CameraBehavior.instance.ShowLeftCornerUI(refPickup.coolantImage, refPickup.coolantTitle, "", PickupTextDuration);
                    break;
                case CheckType.FireRes:
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_FIRE_RES_TITLE", "_FIRE_RES_DESCRIPTION", PickupTextDuration);
                    GameManager.instance.fireRes = true;
                    hasWorldObject = false;
                    break;
                case CheckType.WaterRes:
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_WATER_RES_TITLE", "_WATER_RES_DESCRIPTION", PickupTextDuration);
                    GameManager.instance.waterRes = true;
                    hasWorldObject = false;
                    break;
                case CheckType.TrainStation:
                    CameraBehavior.instance.ShowLeftCornerUI(null, GameManager.instance.trainStations[check.CheckId].stationName, "", PickupTextDuration);
                    GameManager.instance.trainStations[check.CheckId].unlockedStation = true;
                    GameManager.instance.trainUnlocked = true;
                    AchievementManager.instance.CheckNumberOfTrainStationsUnlocked();
                    hasWorldObject = false;
                    break;
                case CheckType.Clock:
                    //This is never randomized, but is important to logic
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

        private static string GetSpoilerText(RandoCheck check)
        {
            switch (check.Type)
            {
                case CheckType.Wrench:
                    return "_HEALING_WRENCH_TITLE";
                case CheckType.Bulblet:
                    return "_LIGHT_BULB_TITLE";
                case CheckType.Ability:
                    var refUnlock = HaikuResources.RefUnlockTutorial;
                    return refUnlock.abilities[check.CheckId].title;
                case CheckType.Item:
                    return InventoryManager.instance.items[check.CheckId].itemName;
                case CheckType.Chip:
                    return GameManager.instance.chip[check.CheckId].title;
                case CheckType.ChipSlot:
                    return "_CHIP_SLOT";
                case CheckType.MapDisruptor:
                    return "_DISRUPTOR";
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
                    return "_POWERCELL";
                case CheckType.Coolant:
                    return "_COOLANT_TITLE";
                case CheckType.FireRes:
                    return "_FIRE_RES_TITLE";
                case CheckType.WaterRes:
                    return "_WATER_RES_TITLE";
                case CheckType.TrainStation:
                    return GameManager.instance.trainStations[check.CheckId].title;
                case CheckType.Clock:
                    //This is never randomized, but is important to logic
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return "";
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
