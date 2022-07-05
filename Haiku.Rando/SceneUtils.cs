using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haiku.Rando
{
    public static class SceneUtils
    {
        private static int _lastSceneId;
        private static GameObject[] _sceneRoots = {};
        
        public static T[] FindObjectsOfType<T>()
            where T: MonoBehaviour
        {
            UpdateRoots();
            return _sceneRoots.Where(r => r).SelectMany(r => r.GetComponentsInChildren<T>(true)).Where(IsValid).ToArray();
        }

        private static void UpdateRoots()
        {
            //Cache roots, but refresh them if first root is dead (this indicates that scene was left and reentered)
            int sceneId = SceneManager.GetActiveScene().buildIndex;
            if (sceneId != _lastSceneId || _sceneRoots.Length == 0 || (!_sceneRoots[0]))
            {
                _lastSceneId = sceneId;
                _sceneRoots = SceneManager.GetActiveScene().GetRootGameObjects();
            }
        }

        private static bool IsValid<T>(T x) where T: MonoBehaviour
        {
            if (x == null) return false;

            return x.isActiveAndEnabled ||
                   typeof(T) == typeof(TrainTicket) ||
                   typeof(T) == typeof(EnterTrain) ||
                   typeof(T) == typeof(EnterRoomTrigger) ||
                   typeof(T) == typeof(PickupBulb) ||
                   typeof(T) == typeof(ShopItemButton) ||
                   IsSpecialPickup(x) ||
                   IsMischievousPowerCell(x) ||
                   IsPortalReward(x) ||
                   x.GetComponents<MonoBehaviour>().Any(c => c.GetType().Name.StartsWith("EnableIf"));
        }

        private static bool IsSpecialPickup<T>(T x) where T: MonoBehaviour
        {
            return x is PickupItem pickup && ((pickup.triggerChip && pickup.chipIdentifier == GameManager.instance.chip[21].identifier) ||
                                              (IsItem(pickup) && (pickup.itemID == 6 || pickup.itemID == 0)));
        }

        private static bool IsItem(PickupItem pickup)
        {
            return !pickup.triggerChip && !pickup.triggerChipSlot && !pickup.triggerCoolant && !pickup.triggerPin;
        }

        private static bool IsMischievousPowerCell<T>(T x) where T: MonoBehaviour
        {
            return x is PowerCell pc && pc.saveID == 16;
        }

        private static bool IsPortalReward<T>(T x) where T : MonoBehaviour
        {
            var portal = Object.FindObjectOfType<e29Portal>();
            if (!portal) return false;

            return x is PickupItem;
        }

        public static T FindObjectOfType<T>()
            where T: MonoBehaviour
        {
            UpdateRoots();
            return _sceneRoots.Where(r => r).Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(IsValid);
        }
    }
}
