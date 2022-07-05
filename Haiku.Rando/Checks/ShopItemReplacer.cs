using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;
using UnityEngine;

namespace Haiku.Rando.Checks
{
    public sealed class ShopItemReplacer : MonoBehaviour
    {
        public RandoCheck check;

        public static void InitHooks()
        {
            On.ShopItemButton.OnEnable += ShopItemButton_OnEnable;
            On.ShopItemButton.UpdateTextsAndCursor += ShopItemButton_UpdateTextsAndCursor;
            On.ShopItemButton.ClickPurchase += ShopItemButton_ClickPurchase;
            On.ShopTrigger.ConfirmPurchase += ShopTrigger_ConfirmPurchase;
            On.ShopItemButton.InitialCheck += ShopItemButton_InitialCheck;
        }

        private static ShopItemReplacer _pendingPurchase;

        private static void ShopTrigger_ConfirmPurchase(On.ShopTrigger.orig_ConfirmPurchase orig, ShopTrigger self)
        {
            var replacer = _pendingPurchase;
            if (!replacer)
            {
                orig(self);
                return;
            }

            //From original game code; skip worldObjects, as that's part of Check triggering
            self.buttonHolder.SetActive(false);
            InventoryManager.instance.SpendSpareParts(self.priceHolder);

            //Trigger the replacement check
            CheckManager.TriggerCheck(replacer, replacer.check);

            //From original game code
            SoundManager.instance.PlayOneShot("event:/UI/UI Success");
            self.Invoke("PlayPurchaseSound", 0.25f);
            self.areYouSureCanvas.SetActive(false);
            self.shopCanvas.SetActive(true);
            if (self.allItemsSold())
            {
                self.CloseShop(false);
                self.gameObject.SetActive(false);
                self.ChangeDialogueTriggersToAllItemsSold();
                return;
            }
            AchievementManager.instance.SetAchievement("_TRADE");
            self.AssignFirstItemToEvents();
        }

        private static void ShopItemButton_ClickPurchase(On.ShopItemButton.orig_ClickPurchase orig, ShopItemButton self)
        {
            var replacer = self.GetComponent<ShopItemReplacer>();
            if (!replacer)
            {
                _pendingPurchase = null;
                orig(self);
                return;
            }

            _pendingPurchase = replacer;
            self.item = true;
            orig(self);
        }

        private static void ShopItemButton_UpdateTextsAndCursor(On.ShopItemButton.orig_UpdateTextsAndCursor orig, ShopItemButton self)
        {
            var replacer = self.GetComponent<ShopItemReplacer>();
            if (!replacer)
            {
                orig(self);
                return;
            }

            var check = replacer.check;
            if (check == null) return;

            string title = "";
            string description = "";
            Sprite image = null;
            switch (check.Type)
            {
                case CheckType.Wrench:
                    title = "_HEALING_WRENCH_TITLE";
                    description = "_HEALING_WRENCH_DESCRIPTION";
                    image = InventoryManager.instance.items[(int)ItemId.Wrench].image;
                    break;
                case CheckType.Bulblet:
                    title = "_LIGHT_BULB_TITLE";
                    description = "_LIGHT_BULB_DESCRIPTION";
                    image = HaikuResources.ItemDesc().lightBulb.image.sprite;
                    break;
                case CheckType.Ability:
                    var refUnlock = HaikuResources.RefUnlockTutorial;
                    title = refUnlock.abilities[check.CheckId].title;
                    description = refUnlock.abilities[check.CheckId].controls;
                    image = refUnlock.abilities[check.CheckId].image; //TODO: Should this grab from menu canvas instead?
                    break;
                case CheckType.Item:
                    title = InventoryManager.instance.items[check.CheckId].itemName;
                    description = InventoryManager.instance.items[check.CheckId].itemDescription;
                    image = InventoryManager.instance.items[check.CheckId].image;
                    break;
                case CheckType.Chip:
                    title = GameManager.instance.chip[check.CheckId].title;
                    description = GameManager.instance.chip[check.CheckId].description;
                    image = GameManager.instance.chip[check.CheckId].image;
                    break;
                case CheckType.ChipSlot:
                    title = "_CHIP_SLOT";
                    description = "_CHIP_SLOT_DESC";
                    image = HaikuResources.GetRefChipSlot(check.CheckId).chipSlotImage;
                    break;
                case CheckType.MapDisruptor:
                    title = "_DISRUPTOR";
                    description = "Add text for disruptor locations here";
                    image = HaikuResources.RefDisruptor.GetComponentInChildren<SpriteRenderer>(true).sprite;
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
                    title = "_POWERCELL";
                    description = "";
                    image = HaikuResources.RefPowerCell.GetComponentInChildren<SpriteRenderer>(true).sprite;
                    break;
                case CheckType.Coolant:
                    title = "_COOLANT_TITLE";
                    description = "_COOLANT_DESCRIPTION";
                    image = HaikuResources.RefPickupCoolant.coolantImage;
                    break;
                case CheckType.TrainStation:
                    title = GameManager.instance.trainStations[check.CheckId].title;
                    description = GameManager.instance.trainStations[check.CheckId].stationName;
                    break;
                case CheckType.FireRes:
                    title = "_FIRE_RES_TITLE";
                    description = "_FIRE_RES_DESCRIPTION";
                    image = HaikuResources.ItemDesc().fireRes.image.sprite;
                    break;
                case CheckType.WaterRes:
                    title = "_WATER_RES_TITLE";
                    description = "_WATER_RES_DESCRIPTION";
                    image = HaikuResources.ItemDesc().waterRes.image.sprite;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            self.shopScript.UpdateTexts(title, description, image, self.price, 0, 0, self.gameObject, self.itemPositionInList);
            self.shopScript.UpdateCursor(self.transform);
        }

        private static void ShopItemButton_InitialCheck(On.ShopItemButton.orig_InitialCheck orig, ShopItemButton self)
        {
            //Initial check is replaced by the Start for this 
            var replacer = self.GetComponent<ShopItemReplacer>();
            if (!replacer)
            {
                orig(self);
                return;
            }

            if (replacer.check == null) return;

            bool alreadyGot = CheckManager.AlreadyGotCheck(replacer.check);
            if (alreadyGot)
            {
                self.gameObject.SetActive(false);
            }
        }

        private static void ShopItemButton_OnEnable(On.ShopItemButton.orig_OnEnable orig, ShopItemButton self)
        {
            var replacer = self.GetComponent<ShopItemReplacer>();
            if (!replacer)
            {
                orig(self);
                return;
            }

            self.item = false;
            self.chip = false;
            self.chipSlot = false;
            self.marker = false;
            orig(self);

            if (replacer.check == null) return;

            switch (replacer.check.Type)
            {
                case CheckType.Wrench:
                    self.itemImage.sprite = InventoryManager.instance.items[(int)ItemId.Wrench].image;
                    break;
                case CheckType.Bulblet:
                    self.itemImage.sprite = HaikuResources.ItemDesc().lightBulb.image.sprite;
                    break;
                case CheckType.Ability:
                    //TODO: From canvas?
                    break;
                case CheckType.Item:
                    self.itemImage.sprite = InventoryManager.instance.items[replacer.check.CheckId].image;
                    break;
                case CheckType.Chip:
                    self.itemImage.sprite = GameManager.instance.chip[replacer.check.CheckId].image;
                    break;
                case CheckType.ChipSlot:
                    self.itemImage.sprite = HaikuResources.GetRefChipSlot(replacer.check.CheckId).chipSlotImage;
                    break;
                case CheckType.MapDisruptor:
                    self.itemImage.sprite = HaikuResources.RefDisruptor.GetComponentInChildren<SpriteRenderer>(true).sprite;
                    break;
                case CheckType.Lore:
                    break;
                case CheckType.Lever:
                    break;
                case CheckType.PartsMonument:
                    break;
                case CheckType.PowerCell:
                    self.itemImage.sprite = HaikuResources.RefPowerCell.GetComponentInChildren<SpriteRenderer>(true).sprite;
                    break;
                case CheckType.Coolant:
                    self.itemImage.sprite = HaikuResources.RefPickupCoolant.coolantImage;
                    break;
                case CheckType.TrainStation:
                    //TODO?
                    break;
                case CheckType.FireRes:
                    self.itemImage.sprite = HaikuResources.ItemDesc().fireRes.image.sprite;
                    break;
                case CheckType.WaterRes:
                    self.itemImage.sprite = HaikuResources.ItemDesc().waterRes.image.sprite;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
