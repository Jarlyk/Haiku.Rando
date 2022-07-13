using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FMODUnity;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Haiku.Rando
{
    public static class QoL
    {
        public static void InitHooks()
        {
            //Haiku Fast Wake
            On.IntroSequence.Intro += IntroSequence_Intro;

            //Remove elevator log spam
            IL.ElevatorPlayerLocManager.PlayerInRange += ElevatorPlayerLocManager_PlayerInRange;

            //Fast Money
            IL.SmallMoneyPile.TakeDamage += SmallMoneyPile_TakeDamage; 

            //Synced Money
            On.SmallMoneyPile.SpawnCurrency += SmallMoneyPile_SpawnCurrency;
            IL.ChildColliderHealth.TakeDamage += ChildColliderHealth_TakeDamage;
            IL.EnemyHealth.TakeDamage += EnemyHealth_TakeDamage;
            On.SwingingGarbageMagnet.SpawnCurrency += SwingingGarbageMagnet_SpawnCurrency;

            //Pre-Broken Doors
            On.BreakableDoor.Start += BreakableDoor_Start;
            On.BreakableDoorWithBackgroundObject.Start += BreakableDoorWithBackgroundObject_Start;
        }

        private static void BreakableDoorWithBackgroundObject_Start(On.BreakableDoorWithBackgroundObject.orig_Start orig, BreakableDoorWithBackgroundObject self)
        {
            if (Settings.PreBrokenDoors.Value)
            {
                GameManager.instance.doors[self.doorID].opened = true;
            }

            orig(self);
        }

        private static void BreakableDoor_Start(On.BreakableDoor.orig_Start orig, BreakableDoor self)
        {
            if (Settings.PreBrokenDoors.Value)
            {
                GameManager.instance.doors[self.ID].opened = true;
            }

            orig(self);
        }

        private static IEnumerator IntroSequence_Intro(On.IntroSequence.orig_Intro orig, IntroSequence self)
        {
            //Fast Haiku Wake sequence
            yield return new WaitForSeconds(0.5f);
            GameManager.instance.introPlayed = true;
            GameManager.instance.gameLoaded = false;
            PlayerScript.instance.isDead = false;
            InventoryManager.instance.AddItem(4);
            self.virusParticles[0].Play();
            self.virusParticles[1].Play();
            CameraBehavior.instance.Shake(6f, 0.05f);
            yield return new WaitForSeconds(0.2f);
            self.puppetMasterAnim.SetTrigger("trigger");
            yield return new WaitForSeconds(0.2f);
            CameraBehavior.instance.Shake(0.2f, 0.1f);
            yield return new WaitForSeconds(0.4f);
            self.anim.SetTrigger("1");
            yield return new WaitForSeconds(0.2f);
            self.anim.SetTrigger("2");
            yield return new WaitForSeconds(0.2f);
            self.anim.SetTrigger("3");
            yield return new WaitForSeconds(0.2f);
            self.anim.SetTrigger("4");
            yield return new WaitForSeconds(0.2f);
            self.anim.SetTrigger("5");
            yield break;
        }

        private static void ElevatorPlayerLocManager_PlayerInRange(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(i => i.MatchLdstr(out _),
                       i => i.MatchCall("UnityEngine.Debug", "Log"));
            var skipLog = c.DefineLabel();
            c.Emit(OpCodes.Br, skipLog);

            c.GotoNext(i => i.MatchRet());
            c.MarkLabel(skipLog);
        }

        private static void SmallMoneyPile_TakeDamage(ILContext il)
        {
            var c = new ILCursor(il);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Func<SmallMoneyPile, bool>)OnSmallMoneyPileTakeDamage);
            var retLabel = c.DefineLabel();
            c.Emit(OpCodes.Brtrue, retLabel);

            c.GotoNext(i => i.MatchRet());
            c.MarkLabel(retLabel);
        }

        private static bool OnSmallMoneyPileTakeDamage(SmallMoneyPile self)
        {
            if (!Settings.FastMoney.Value)
            {
                return false;
            }

            if (self.flipSpriteWhenHit)
            {
                self.CheckPlayerPosAndFlip();
            }

            for (int i = 0; i < self.health; i++)
            {
                self.SpawnCurrency();
            }

            GameManager.instance.moneyPiles[self.pileID].collected = true;
            if (self.hitSFXPath != "")
            {
                RuntimeManager.PlayOneShot(self.hitSFXPath, self.transform.position);
            }

            self.health = 0;
            self.coll.enabled = false;
            self.SpawnCurrency();
            CameraBehavior.instance.Shake(0.2f, 0.2f);
            self.anim.SetTrigger("hit");
            self.anim.SetTrigger("damaged");
            self.anim.SetTrigger("more damaged");
            self.anim.SetBool("depleted", true);
            return true;
        }

        private static void SmallMoneyPile_SpawnCurrency(On.SmallMoneyPile.orig_SpawnCurrency orig, SmallMoneyPile self)
        {
            if (!Settings.SyncedMoney.Value)
            {
                orig(self);
                return;
            }

            var rng = SyncedRng.Get(self.gameObject);
            Object.Instantiate(self.explodeEffect, self.transform.position, Quaternion.identity);
            var pick1 = rng.Random.NextRange(0, self.currencies.Length);
            var pick2 = rng.Random.NextRange(0, self.currencies.Length);
            Object.Instantiate(self.currencies[pick1], self.transform.position, Quaternion.identity);
            Object.Instantiate(self.currencies[pick2], self.transform.position, Quaternion.identity);
        }

        private static void ChildColliderHealth_TakeDamage(ILContext il)
        {
            var c = new ILCursor(il);

            //Replace the existing currency spawning
            c.GotoNext(i => i.MatchLdarg(0),
                       i => i.MatchLdfld("ChildColliderHealth", "currencies"));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Action<ChildColliderHealth>)DropCurrencySynced);
            var skipExisting = c.DefineLabel();
            c.Emit(OpCodes.Br, skipExisting);

            //We jump to the Die() call, bypassing the existing code
            c.GotoNext(i => i.MatchLdarg(0),
                       i => i.MatchCall("ChildColliderHealth", "Die"));
            c.MarkLabel(skipExisting);
        }

        private static void DropCurrencySynced(ChildColliderHealth self)
        {
            int pick1;
            int pick2;
            if (Settings.SyncedMoney.Value)
            {
                var rng = SyncedRng.Get(self.gameObject);
                pick1 = rng.Random.NextRange(0, self.currencies.Length);
                pick2 = rng.Random.NextRange(0, self.currencies.Length);
            }
            else
            {
                pick1 = Random.Range(0, self.currencies.Length);
                pick2 = Random.Range(0, self.currencies.Length);
            }
            Object.Instantiate(self.currencies[pick1], self.transform.position, Quaternion.identity);
            Object.Instantiate(self.currencies[pick2], self.transform.position, Quaternion.identity);
        }

        private static void EnemyHealth_TakeDamage(ILContext il)
        {
            var c = new ILCursor(il);

            //Replace the existing currency spawning
            ILLabel skipExisting = null;
            c.GotoNext(MoveType.After,
                       i => i.MatchLdarg(0),
                       i => i.MatchLdfld("EnemyHealth", "currencies"),
                       i => i.MatchLdlen(),
                       i => i.MatchBrfalse(out skipExisting));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((Action<EnemyHealth>)EHDropCurrencySynced);
            c.Emit(OpCodes.Br, skipExisting);
        }

        private static void EHDropCurrencySynced(EnemyHealth self)
        {
            int pick1;
            int pick2;
            if (Settings.SyncedMoney.Value)
            {
                var rng = SyncedRng.Get(self.gameObject);
                pick1 = rng.Random.NextRange(0, self.currencies.Length);
                pick2 = rng.Random.NextRange(0, self.currencies.Length);
            }
            else
            {
                pick1 = Random.Range(0, self.currencies.Length);
                pick2 = Random.Range(0, self.currencies.Length);
            }
            Object.Instantiate(self.currencies[pick1], self.transform.position, Quaternion.identity);
            Object.Instantiate(self.currencies[pick2], self.transform.position, Quaternion.identity);
        }

        private static void SwingingGarbageMagnet_SpawnCurrency(On.SwingingGarbageMagnet.orig_SpawnCurrency orig, SwingingGarbageMagnet self)
        {
            if (!Settings.SyncedMoney.Value)
            {
                orig(self);
                return;
            }

            var rng = SyncedRng.Get(self.gameObject);
            for (int i = 0; i < 6; i++)
            {
                var pick = rng.Random.NextRange(0, self.currencies.Length);
                Object.Instantiate(self.currencies[pick], self.transform.position, Quaternion.identity);
            }
        }
    }
}
