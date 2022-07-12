using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Haiku.Rando
{
    public sealed class RepairStationWarpButton : MonoBehaviour, ISelectHandler
    {
        public RepairStationWarp owner;
        public int sceneId;
        public bool isHaikuWake;

        private void Start()
        {
            var button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);
        }
        public void OnSelect(BaseEventData eventData)
        {
            owner.UpdateCursor(this);
        }

        private void OnClick()
        {
            Debug.Log($"Clicked on {gameObject.name}");
        }
    }
}
