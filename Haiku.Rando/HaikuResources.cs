using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Haiku.Rando
{
    public static class HaikuResources
    {
        public static GameObject PrefabGenericPickup { get; private set; }

        public static PickupItem RefPickupItem { get; private set; }

        public static UnlockTutorial RefUnlockTutorial { get; private set; }

        public static Disruptor RefDisruptor { get; private set; }

        public static void Init()
        {
            //TODO: Load from resources

            //TODO: Create reference instances for grabbing nested resource references
        }
    }
}
