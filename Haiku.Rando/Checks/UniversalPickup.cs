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
            //TODO: Check whether PickupItem.Start interacts properly in conjunction with this

            On.PickupItem.TriggerPickup += PickupItemOnTriggerPickup;
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
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_HEALING_WRENCH_TITLE", "", 4f);
                    GameManager.instance.canHeal = true;
                    GameManager.instance.worldObjects[check.SaveId].collected = true;
                    break;
                case CheckType.Bulblet:
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_LIGHT_BULB_TITLE", "", 4f);
                    GameManager.instance.lightBulb = true;
                    break;
                case CheckType.Ability:
                    //TODO: Maybe this should be handled by actual ability pickup object?
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
                case CheckType.MapDisruptor:
                    //TODO: This one should probably be replaced with the actual disruptor
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
                    self.StartCoroutine(CheckManager.RemoveHeat());
                    GameManager.instance.powerCells[check.CheckId].collected = true;
                    //TODO: Sound effect and particles from PowerCell?
                    AchievementManager.instance.CheckNumbersOfPowercellsCollected();
                    break;
                case CheckType.Coolant:
                    self.triggerCoolant = true;
                    orig(self);
                    break;
                case CheckType.FireRes:
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_FIRE_RES_TITLE", "_FIRE_RES_DESCRIPTION", 4f);
                    GameManager.instance.fireRes = true;
                    break;
                case CheckType.WaterRes:
                    CameraBehavior.instance.ShowLeftCornerUI(null, "_WATER_RES_TITLE", "_WATER_RES_DESCRIPTION", 4f);
                    GameManager.instance.waterRes = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Start()
        {
            if (check == null) return;

            bool alreadyGot = CheckManager.AlreadyGotCheck(check);
            if (alreadyGot)
            {
                gameObject.SetActive(false);
            }

            if (midAir)
            {
                var pickup = GetComponent<PickupItem>();
                pickup.interactAnimator.enabled = false;
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