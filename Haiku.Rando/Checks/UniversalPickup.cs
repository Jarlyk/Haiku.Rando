using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Haiku.Rando.Topology;
using UnityEngine;

namespace Haiku.Rando.Checks
{
    /// <summary>
    /// This component handles all in-world checks as a 'shiny' pickup;
    /// it's designed to provide additional context to an existing PickupItem
    /// component.
    /// </summary>
    public sealed class UniversalPickup : MonoBehaviour
    {
        public RandoCheck check;
        public bool midAir;

        public static void InitHooks()
        {
            On.PickupItem.Start += PickupItem_Start;
            On.PickupItem.TriggerPickup += PickupItemOnTriggerPickup;
        }

        private static void PickupItem_Start(On.PickupItem.orig_Start orig, PickupItem self)
        {
            var universalPickup = self.GetComponent<UniversalPickup>();
            if (!universalPickup)
            {
                orig(self);
                return;
            }

            var check = universalPickup.check;
            if (check == null) return;

            bool alreadyGot = CheckManager.AlreadyGotCheck(check);
            if (alreadyGot)
            {
                self.gameObject.SetActive(false);
            }

            if (universalPickup.midAir)
            {
                self.interactAnimator.enabled = false;
            }
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

            self.triggerChip = false;
            self.triggerChipSlot = false;
            self.triggerCoolant = false;
            self.triggerPin = false;
            self.saveID = check.SaveId;
            self.collected = true;

            switch (check.Type)
            {
                case CheckType.Wrench:
                case CheckType.Bulblet:
                case CheckType.Ability:
                case CheckType.MapDisruptor:
                case CheckType.Lore:
                case CheckType.Lever:
                case CheckType.PartsMonument:
                case CheckType.PowerCell:
                case CheckType.FireRes:
                case CheckType.WaterRes:
                case CheckType.TrainStation:
                    CheckManager.TriggerCheck(self, check);
                    break;
                case CheckType.Item:
                    self.itemID = check.CheckId;
                    orig(self);
                    break;
                case CheckType.Chip:
                    self.triggerChip = true;
                    self.chipIdentifier = GameManager.instance.chip[check.CheckId].identifier;
                    orig(self);
                    break;
                case CheckType.ChipSlot:
                    self.triggerChipSlot = true;
                    self.chipSlotNumber = check.CheckId;
                    orig(self);
                    break;
                case CheckType.Coolant:
                    self.triggerCoolant = true;
                    orig(self);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
    }
}