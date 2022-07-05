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

        private void Start()
        {
            //PickupItem might have already run Start, so we need to repeat the active-setting process
            OnStart(GetComponent<PickupItem>());
        }

        private void OnStart(PickupItem pickup)
        {
            if (check == null) return;

            bool alreadyGot = CheckManager.AlreadyGotCheck(check);
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

            CheckManager.TriggerCheck(self, check);
            self.gameObject.SetActive(false);
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