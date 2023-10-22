using System;
using System.Linq;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class SplunkRewardReplacer : MonoBehaviour
    {
        public IRandoItem replacement;

        public static void InitHooks()
        {
            On.SplunkOnBench.EndDialogueAction += ReplaceItem;
        }

        private static void ReplaceItem(On.SplunkOnBench.orig_EndDialogueAction orig, SplunkOnBench self)
        {
            if (self.giveSparePartsBack && self.GetComponent<SplunkRewardReplacer>() is {} replacer)
            {
                replacer.replacement.Trigger(self);
            }
            else
            {
                orig(self);
            }
        }

        public static void ReplaceCheck(IRandoItem replacement)
        {
            var splunk = SceneUtils.FindObjectsOfType<SplunkOnBench>()
                .Where(s => !s.giveSparePartsBack).FirstOrDefault();
            if (splunk == null)
            {
                throw new InvalidOperationException("tried to replace Splunk but they were not present");
            }
            var srr = splunk.givePartsBackObj.gameObject.AddComponent<SplunkRewardReplacer>();
            srr.replacement = replacement;
            srr.enabled = true;
        }
    }
}