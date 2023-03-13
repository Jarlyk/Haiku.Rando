using System;
using System.IO;
using System.Reflection;
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

            var uidef = UIDef.Of(check);
            self.shopScript.UpdateTexts(uidef.Name, uidef.Description, uidef.Sprite, self.price, 0, 0, self.gameObject, self.itemPositionInList);
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
            var sprite = UIDef.Of(replacer.check).Sprite;
            if (sprite == null) return;
            self.itemImage.sprite = sprite;
        }
    }
}
