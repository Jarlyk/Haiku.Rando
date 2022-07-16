using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using UnityEngine;

namespace Haiku.Rando
{
    public sealed class TransitionManager
    {
        public static readonly TransitionManager Instance = new TransitionManager();

        public TransitionRandomizer Randomizer { get; set; }

        public static void InitHooks()
        {
            On.EnterRoomTrigger.Start += EnterRoomTrigger_Start;
            On.LoadNewLevel.Awake += LoadNewLevel_Awake;
        }

        private static void EnterRoomTrigger_Start(On.EnterRoomTrigger.orig_Start orig, EnterRoomTrigger self)
        {
            orig(self);

            var randomizer = Instance.Randomizer;
            if (randomizer != null)
            {
                var outgoing = self.transform.localScale.x >= 1f;
                var node1 = randomizer.Swaps.Keys.FirstOrDefault(n => n.Name == self.pointName);
                if (node1 != null)
                {
                    var oldName = self.pointName;
                    var node2 = randomizer.Swaps[node1];
                    self.pointName = node2.Name;
                    self.levelToLoad = outgoing ? node1.SceneId2 : node2.SceneId1;
                    Debug.Log($"Reconfiguring {oldName} door transition to {self.levelToLoad}:{self.pointName}");
                }
            }
        }

        private static void LoadNewLevel_Awake(On.LoadNewLevel.orig_Awake orig, LoadNewLevel self)
        {
            orig(self);

            var randomizer = Instance.Randomizer;
            if (randomizer != null)
            {
                var outgoing = self.transform.localScale.x >= 1f;
                var node1 = randomizer.Swaps.Keys.FirstOrDefault(n => n.Name == self.pointName);
                if (node1 != null)
                {
                    var oldName = self.pointName;
                    var node2 = randomizer.Swaps[node1];
                    self.pointName = node2.Name;
                    self.levelToLoad = outgoing ? node1.SceneId2 : node2.SceneId1;
                    Debug.Log($"Redirecting {oldName} edge transition to {self.levelToLoad}:{self.pointName}");
                }
            }
        }
    }
}
