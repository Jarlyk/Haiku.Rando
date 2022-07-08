using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FMODUnity;
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

            //Fast Money
            On.SmallMoneyPile.TakeDamage += SmallMoneyPile_TakeDamage;

            //Synced Money
            On.SmallMoneyPile.SpawnCurrency += SmallMoneyPile_SpawnCurrency;
            IL.ChildColliderHealth.TakeDamage += ChildColliderHealth_TakeDamage;
            IL.EnemyHealth.TakeDamage += EnemyHealth_TakeDamage;
            On.SwingingGarbageMagnet.SpawnCurrency += SwingingGarbageMagnet_SpawnCurrency;
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

        private static void SmallMoneyPile_TakeDamage(On.SmallMoneyPile.orig_TakeDamage orig, SmallMoneyPile self, int damage, int side, object playerPos)
        {
            if (!Settings.FastMoney.Value)
            {
                orig(self, damage, side, playerPos);
                return;
            }

            if (self.flipSpriteWhenHit)
            {
                self.CheckPlayerPosAndFlip();
            }

            for (int i = 0; i < self.health; i++)
            {
                self.SpawnCurrency();
            }

            if (self.hitSFXPath != "")
            {
                RuntimeManager.PlayOneShot(self.hitSFXPath, self.transform.position);
            }

            self.health = 0;
            self.coll.enabled = false;
            self.SpawnCurrency();
            CameraBehavior.instance.Shake(0.2f, 0.2f);
            self.anim.SetBool("depleted", true);
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
            //TODO
        }

        private static void SwingingGarbageMagnet_SpawnCurrency(On.SwingingGarbageMagnet.orig_SpawnCurrency orig, SwingingGarbageMagnet self)
        {
            orig(self);
            //TODO
        }
    }
}
