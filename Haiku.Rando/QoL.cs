using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Haiku.Rando
{
    public static class QoL
    {
        public static void InitHooks()
        {
            On.IntroSequence.Intro += IntroSequence_Intro;
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
    }
}
