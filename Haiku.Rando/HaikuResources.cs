using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Haiku.Rando
{
    public static class HaikuResources
    {
        public static GameObject PrefabGenericPickup { get; private set; }

        public static GameObject PrefabBigMoneyPile { get; private set; }

        public static GameObject PrefabSmallMoneyPile { get; private set; }

        public static GameObject PrefabMoneyString { get; private set; }

        public static PickupItem RefPickupItem { get; private set; }

        public static PickupItem RefPickupCoolant { get; private set; }

        public static PickupItem RefPickupRedChipSlot { get; private set; }

        public static PickupItem RefPickupBlueChipSlot { get; private set; }

        public static PickupItem RefPickupGreenChipSlot { get; private set; }

        public static PowerCell RefPowerCell { get; private set; }

        public static UnlockTutorial RefUnlockTutorial { get; private set; }

        public static Disruptor RefDisruptor { get; private set; }

        public static void Init()
        {
            PrefabGenericPickup = Resources.Load<GameObject>("PickupItemTrigger 1");
            PrefabBigMoneyPile = Resources.Load<GameObject>("BigMoneyPileHolder 1");
            PrefabSmallMoneyPile = Resources.Load<GameObject>("SmallMoneyPileHolder 1");
            PrefabMoneyString = Resources.Load<GameObject>("StringOfCogs 1");

            RefPickupItem = LoadRef<PickupItem>("PickupItemTrigger 1");
            RefPickupCoolant = LoadRef<PickupItem>("PickupCoolantTrigger 1");
            RefPickupRedChipSlot = LoadRef<PickupItem>("PickupRedChipSlotTrigger 1");
            RefPickupBlueChipSlot = LoadRef<PickupItem>("PickupBlueChipSlotTrigger 1");
            RefPickupGreenChipSlot = LoadRef<PickupItem>("PickupGreenChipSlotTrigger 1");
            RefPowerCell = LoadRef<PowerCell>("PowerCell 1");
            RefUnlockTutorial = LoadRef<UnlockTutorial>("PickupPREFAB 1");
            RefDisruptor = LoadRef<Disruptor>("Disruptor 1");
        }

        public static PickupItem GetRefChipSlot(int chipSlotId)
        {
            var slot = GameManager.instance.chipSlot[chipSlotId];
            if (slot.chipSlotColor == "red") return RefPickupRedChipSlot;
            if (slot.chipSlotColor == "green") return RefPickupGreenChipSlot;
            if (slot.chipSlotColor == "blue") return RefPickupBlueChipSlot;
            throw new ArgumentException($"Invalid color type '{slot.chipSlotColor}' for id {chipSlotId}");
        }

        private static T LoadRef<T>(string resourcePath)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            var refInstance = Object.Instantiate(prefab);
            refInstance.SetActive(false);
            Object.DontDestroyOnLoad(refInstance);
            return refInstance.GetComponent<T>();
        }

        private static GameObject _itemDescObject;

        public static ItemDescriptionManager ItemDesc()
        {
            if (!_itemDescObject)
            {
                _itemDescObject = GetDontDestroyOnLoadObjects()
                                  .Select(x => x.GetComponentInChildren<ItemDescriptionManager>(true))
                                  .First(d => d).gameObject;
            }

            return _itemDescObject.GetComponent<ItemDescriptionManager>();
        }

        private static GameObject[] GetDontDestroyOnLoadObjects()
        {
            GameObject temp = null;
            try
            {
                temp = new GameObject();
                Object.DontDestroyOnLoad( temp );
                UnityEngine.SceneManagement.Scene dontDestroyOnLoad = temp.scene;
                Object.DestroyImmediate( temp );
                temp = null;
     
                return dontDestroyOnLoad.GetRootGameObjects();
            }
            finally
            {
                if( temp != null )
                    Object.DestroyImmediate( temp );
            }
        }
    }
}
