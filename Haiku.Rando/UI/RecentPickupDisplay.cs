using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Text;
using Modding;
using UnityEngine;
using UnityEngine.UI;

namespace Haiku.Rando.UI
{
    public sealed class RecentPickupDisplay : MonoBehaviour
    {
        public static RecentPickupDisplay instance;

        private const int EntryCount = 5;

        private GameObject canvas;
        private GameObject panel;
        private GameObject[] _entryPanels;
        private Image[] _imageEntries;
        private Text[] _textEntries;

        void Start()
        {
            instance = this;

            canvas = CanvasUtil.CreateCanvas(1);
            canvas.name = "RecentPickupsCanvas";
            canvas.transform.SetParent(gameObject.transform);
            panel = CanvasUtil.CreateBasePanel(canvas,
                                               new CanvasUtil.RectData(new Vector2(120, -100),
                                                                       new Vector2(-10, 10),
                                                                       new Vector2(1, 0),
                                                                       new Vector2(1, 1)));
            panel.name = "RecentPickupsPanel";

            _entryPanels = new GameObject[EntryCount];
            _imageEntries = new Image[EntryCount];
            _textEntries = new Text[EntryCount];
            for (int i = 0; i < EntryCount; i++)
            {
                _entryPanels[i] = CanvasUtil.CreateBasePanel(panel,
                                                             new CanvasUtil.RectData(new Vector2(0, 16),
                                                                 new Vector2(0, -17*i),
                                                                 new Vector2(0, 1),
                                                                 new Vector2(1, 1)));
                _entryPanels[i].name = $"EntryPanel{i}";

                var imagePanel = CanvasUtil.CreateImagePanel(_entryPanels[i], null,
                                                             new CanvasUtil.RectData(new Vector2(16, 0),
                                                                 new Vector2(0, 0),
                                                                 new Vector2(0, 0),
                                                                 new Vector2(0, 1)));
                _imageEntries[i] = imagePanel.GetComponent<Image>();
                _imageEntries[i].enabled = false;
                imagePanel.name = $"ImagePanel{i}";

                var textPanel = CanvasUtil.CreateTextPanel(_entryPanels[i], "", 7, TextAnchor.MiddleLeft,
                                                           new CanvasUtil.RectData(new Vector2(-16, 0),
                                                               new Vector2(0, 0),
                                                               new Vector2(0, 0),
                                                               new Vector2(1, 1)),
                                                           CanvasUtil.GameFont);
                _textEntries[i] = textPanel.GetComponent<Text>();
                _textEntries[i].verticalOverflow = VerticalWrapMode.Overflow;
                textPanel.name = $"TextPanel{i}";
            }
        }

        void Update()
        {
            var showRecentPickups = Settings.ShowRecentPickups.Value;
            if (showRecentPickups != panel.activeSelf)
            {
                panel.SetActive(showRecentPickups);
            }
        }

        public static void AddRecentPickup(Sprite image, string title, string where)
        {
            if (instance)
            {
                instance.DoAddRecentPickup(image, title, where);
            }
        }

        private void DoAddRecentPickup(Sprite image, string title, string where)
        {
            //Push existing entries down the list
            for (int i = EntryCount - 1; i >= 1; i--)
            {
                _textEntries[i].text = _textEntries[i - 1].text;
                _imageEntries[i].sprite = _imageEntries[i - 1].sprite;
                _imageEntries[i].enabled = _imageEntries[i].sprite != null;
            }

            var text = title.StartsWith("_") ? LocalizationSystem.GetLocalizedValue(title) : title;
            if (where != null)
            {
                text = text + "\nfrom " + where;
            }

            _textEntries[0].text = text;
            _imageEntries[0].sprite = image;
            _imageEntries[0].enabled = _imageEntries[0].sprite != null;
        }

        public static void SetVisible(bool visible)
        {
            if (instance)
            {
                instance.canvas.SetActive(visible);
            }
        }
    }
}
