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
                var src = new TransitionSource(
                    self.gameObject.scene.buildIndex,
                    self.levelToLoad,
                    self.pointName
                );

                if (randomizer.Redirects.TryGetValue(src, out var dest))
                {
                    Debug.Log($"Reconfiguring {self.pointName} door transition to {dest.ToScene}:{dest.ToTransition}");
                    self.levelToLoad = dest.ToScene;
                    self.pointName = dest.ToTransition;
                }
            }
        }

        private static void LoadNewLevel_Awake(On.LoadNewLevel.orig_Awake orig, LoadNewLevel self)
        {
            orig(self);

            var randomizer = Instance.Randomizer;
            if (randomizer != null)
            {
                var src = new TransitionSource(
                    self.gameObject.scene.buildIndex,
                    self.levelToLoad,
                    self.pointName
                );

                if (randomizer.Redirects.TryGetValue(src, out var dest))
                {
                    Debug.Log($"Reconfiguring {self.pointName} edge transition to {dest.ToScene}:{dest.ToTransition}");
                    self.levelToLoad = dest.ToScene;
                    self.pointName = dest.ToTransition;
                }
            }
        }
    }
}
