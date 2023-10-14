using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class QuaternRewardReplacer : MonoBehaviour
    {
        private class Replacement
        {
            public IRandoItem Check;
            public GameObject Pickup;
        }

        private Replacement chipReplacement;
        private Replacement capsule1Replacement;
        private Replacement capsule2Replacement;

        public static void InitHooks()
        {
            On.e29Portal.GiveRewardsGradually += ReplaceRewards;
        }

        private static IEnumerator ReplaceRewards(On.e29Portal.orig_GiveRewardsGradually orig, e29Portal self, int startCount)
        {
            var replacer = self.GetComponent<QuaternRewardReplacer>();
            var result = orig(self, startCount);
            if (!result.MoveNext())
            {
                yield break;
            }
            var running = false;
            do
            {
                yield return result.Current;
                running = result.MoveNext();
                // lastPowercellCount is set to i+1 on each iteration of the
                // original loop
                var i = GameManager.instance.lastPowercellCount - 1;
                var rewardObj = self.rewardObjects[i];
                Debug.Log($"Reward object {i} is {rewardObj.name}");
                if (rewardObj.name.Contains("_Chip"))
                {
                    var r = replacer?.chipReplacement;
                    if (r != null)
                    {
                        var unclaimed = !r.Check.Obtained();
                        rewardObj.SetActive(unclaimed);
                        r.Pickup.SetActive(unclaimed);
                    }
                }
                else if (rewardObj.name.Contains("_Health fragment 1"))
                {
                    var r = replacer?.capsule1Replacement;
                    if (r != null)
                    {
                        r.Pickup.SetActive(!r.Check.Obtained());
                    }
                }
                else if (rewardObj.name.Contains("_Health fragment 2"))
                {
                    var r = replacer?.capsule2Replacement;
                    if (r != null)
                    {
                        r.Pickup.SetActive(!r.Check.Obtained());
                    }
                }
            }
            while (running);
        }

        public static void ReplaceCheck(RandoCheck orig, IRandoItem replacement, GameObject pickup)
        {
            var portal = SceneUtils.FindObjectOfType<e29Portal>();
            if (portal == null)
            {
                throw new InvalidOperationException("attempted to replace Quatern check while e29Portal is not present");
            }
            if (!portal.gameObject.TryGetComponent<QuaternRewardReplacer>(out var replacer))
            {
                replacer = portal.gameObject.AddComponent<QuaternRewardReplacer>();
            }
            switch (orig.Alias)
            {
                case "Item[3]0":
                    replacer.capsule1Replacement = new() { Check = replacement, Pickup = pickup };
                    break;
                case "Item[3]1":
                    replacer.capsule2Replacement = new() { Check = replacement, Pickup = pickup };
                    break;
                case "Chip":
                    replacer.chipReplacement = new() { Check = replacement, Pickup = pickup };
                    break;
                default:
                    throw new InvalidOperationException($"{orig.Alias} is not a Quatern check");
            }
            // This will cause e29PortalRewardChecker.CheckReward to do nothing
            // for checks that are randomized.
            var rewardObj = SceneUtils.FindObjectsOfType<e29PortalRewardChecker>()
                .FirstOrDefault(c => c.objectSaveID == orig.SaveId);
            if (rewardObj != null)
            {
                rewardObj.neededPowercells = 999999;
            }
        }
    }
}