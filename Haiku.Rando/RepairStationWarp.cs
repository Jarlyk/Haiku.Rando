using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FMOD;
using Haiku.Rando.Util;
using Modding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Haiku.Rando
{
    public sealed class RepairStationWarp : MonoBehaviour
    {
        private static GameObject warpUI;

        public RectTransform selectedIndicator;
        private Vector2 cursorMin;
        private Vector2 cursorMax;

        private void OnEnable()
        {
            StartCoroutine(this.MoveCursorOnFirstLoad());
        }

        private IEnumerator MoveCursorOnFirstLoad()
        {
            yield return null;
            var firstButton = GetComponentsInChildren<RepairStationWarpButton>().First().gameObject;
            CameraBehavior.instance.SwapEventSystemButton(firstButton);
        }

        private void LateUpdate()
        {
            this.MoveCursor();
        }

        // Token: 0x06000D6B RID: 3435 RVA: 0x000414F0 File Offset: 0x0003F6F0
        private void MoveCursor()
        {
            float x = Mathf.Lerp(this.selectedIndicator.anchorMin.x, this.cursorMin.x, 10f * Time.unscaledDeltaTime);
            float y = Mathf.Lerp(this.selectedIndicator.anchorMin.y, this.cursorMin.y, 10f * Time.unscaledDeltaTime);
            this.selectedIndicator.anchorMin = new Vector2(x, y);
            float x2 = Mathf.Lerp(this.selectedIndicator.anchorMax.x, this.cursorMax.x, 10f * Time.unscaledDeltaTime);
            float y2 = Mathf.Lerp(this.selectedIndicator.anchorMax.y, this.cursorMax.y, 10f * Time.unscaledDeltaTime);
            this.selectedIndicator.anchorMax = new Vector2(x2, y2);
        }

        public void UpdateCursor(RepairStationWarpButton button)
        {
            float oscX = 0.003f*Mathf.Cos(0.5f*Mathf.PI*Time.unscaledTime);
            var rect = button.GetComponent<RectTransform>();
            cursorMin = rect.anchorMin - new Vector2(0.02f + oscX, 0);
            cursorMax = rect.anchorMax + new Vector2(oscX, 0);
            SoundManager.instance.PlayOneShot("event:/UI/Move cursor");
        }

        public static void InitHooks()
        {
            On.CameraBehavior.Start += CameraBehavior_Start;
            On.CameraBehavior.NextUICanvas += CameraBehavior_NextUICanvas;
            On.CameraBehavior.PreviousUICanvas += CameraBehavior_PreviousUICanvas;
            On.CameraBehavior.ResumeHideUI += CameraBehavior_ResumeHideUI;
        }

        private static void CameraBehavior_ResumeHideUI(On.CameraBehavior.orig_ResumeHideUI orig, CameraBehavior self)
        {
            if (self.pauseUI.activeSelf || self.areYouSureUI.activeSelf)
            {
                self.HideCursor();
            }
            if (!self.inventoryUI.activeSelf && !self.chipsUI.activeSelf && !self.mapUI.activeSelf && !self.pauseUI.activeSelf && !self.areYouSureUI.activeSelf && !self.allChipsUI.activeSelf && !warpUI.activeSelf)
            {
                return;
            }
            if (self.inventoryUI.activeSelf)
            {
                self.inventoryUI.SetActive(false);
            }
            if (self.chipsUI.activeSelf)
            {
                self.chipsUI.SetActive(false);
                self.chipsFadeTransition.SetActive(false);
            }
            if (self.allChipsUI.activeSelf)
            {
                self.allChipsUI.SetActive(false);
                self.chipsUI.SetActive(true);
                self.chipsFadeTransition.SetActive(true);
                return;
            }
            if (self.mapUI.activeSelf)
            {
                self.mapUI.SetActive(false);
            }
            if (self.pauseUI.activeSelf)
            {
                self.pauseUI.SetActive(false);
                GameManager.instance.pauseTimePlayed = false;
            }
            if (self.areYouSureUI.activeSelf)
            {
                self.areYouSureUI.SetActive(false);
            }
            if (warpUI.activeSelf)
            {
                warpUI.SetActive(false);
            }

            TimeManager.instance.paused = false;
            Time.timeScale = 1f;
            GameManager.instance.SwapPlayerAndUIControls("Player Actions");            
        }

        private static void CameraBehavior_PreviousUICanvas(On.CameraBehavior.orig_PreviousUICanvas orig, CameraBehavior self)
        {
            if (warpUI.activeSelf)
            {
                warpUI.SetActive(false);
                self.chipsUI.SetActive(true);
                self.chipsFadeTransition.SetActive(false);
                self.chipsGlitchTransition.SetActive(true);
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
            }
            else if (self.mapUI.activeSelf)
            {
                self.mapUI.SetActive(false);
                warpUI.SetActive(true);
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
            }
            else if (self.inventoryUI.activeSelf)
            {
                self.inventoryUI.SetActive(false);
                self.mapUI.SetActive(true);
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
            }
            else if (self.chipsUI.activeSelf)
            {
                self.chipsUI.SetActive(false);
                self.inventoryUI.SetActive(true);
                self.DisplayHaikuPoweredUpAnimation();
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
                self.SwapEventSystemButton(self.inventoryItemButton);
            }
            else if (self.allChipsUI.activeSelf)
            {
                self.allChipsUI.SetActive(false);
                self.inventoryUI.SetActive(true);
                self.DisplayHaikuPoweredUpAnimation();
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
                self.SwapEventSystemButton(self.inventoryItemButton);
            }
        }

        private static void CameraBehavior_NextUICanvas(On.CameraBehavior.orig_NextUICanvas orig, CameraBehavior self)
        {
            if (warpUI.activeSelf)
            {
                warpUI.SetActive(false);
                self.mapUI.SetActive(true);
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
            }
            else if (self.mapUI.activeSelf)
            {
                self.mapUI.SetActive(false);
                self.inventoryUI.SetActive(true);
                self.DisplayHaikuPoweredUpAnimation();
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
                self.SwapEventSystemButton(self.inventoryItemButton);
            }
            else if (self.inventoryUI.activeSelf)
            {
                self.inventoryUI.SetActive(false);
                self.chipsUI.SetActive(true);
                self.chipsFadeTransition.SetActive(false);
                self.chipsGlitchTransition.SetActive(true);
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
            }
            else if (self.chipsUI.activeSelf)
            {
                self.chipsUI.SetActive(false);
                warpUI.SetActive(true);
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
            }
            else if (self.allChipsUI.activeSelf)
            {
                self.allChipsUI.SetActive(false);
                warpUI.SetActive(true);
                SoundManager.instance.PlayOneShot("event:/UI/Glitch Transition");
            }
        }

        private static void CameraBehavior_Start(On.CameraBehavior.orig_Start orig, CameraBehavior self)
        {
            orig(self);

            LocalizationSystem.localizedEN.Add("_WARP", "W A R P");
            LocalizationSystem.localizedEN.Add("_WARP_ACTION", "Warp {action:UISubmit}");
            LocalizationSystem.localizedEN.Add("_WARP_HEADER", "Warp to a previously visited save point");

            var mapCanvas = self.transform.Find("MapCanvas").gameObject;
            var warpCanvas = Instantiate(mapCanvas, mapCanvas.transform.parent);
            warpUI = warpCanvas;

            //Rename to help keep track of things better
            warpCanvas.name = "WarpCanvas";
            var panel = warpCanvas.transform.GetChild(0).gameObject;
            panel.name = "WarpPanel";

            //Remove map-specific UI elements
            Destroy(panel.transform.Find("Mask").gameObject);
            Destroy(panel.transform.Find("MarkersSideSectionPanel").gameObject);
            Destroy(panel.transform.Find("MapPercentage Text (TMP)").gameObject);

            var midTitle = panel.transform.Find("Panel Title Text");
            var leftTitle = panel.transform.Find("Left Panel Title Text");
            var rightTitle = panel.transform.Find("Right Panel Title Text");

            var midText = midTitle.GetComponent<Text>();
            midText.text = "_WARP";

            var leftText = leftTitle.GetComponent<Text>();
            leftText.text = "_CHIPS";

            var rightText = rightTitle.GetComponent<Text>();
            rightText.text = "_MAP";

            //Replace background with opaque area to put UI
            var bgTexture = TextureUtils.LoadEmbedded("WarpPanel.png", 384, 216);
            bgTexture.filterMode = FilterMode.Point;
            var bgImage = panel.GetComponent<Image>();
            var oldSprite = bgImage.sprite;
            bgImage.sprite = Sprite.Create(bgTexture, oldSprite.rect, new Vector2(0.5f, 0.5f), oldSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);

            //Replace 'Zoom' with 'Warp'
            SetTextTmp(panel, "Zoom Text (TMP)", "_WARP_ACTION");

            //Update other text so that it resyncs with input method
            SetTextTmp(panel, "Exit Text (TMP)", "_EXIT_ACTION");
            SetTextTmp(panel, "Left Text (TMP)", "{action:PreviousPanel}");
            SetTextTmp(panel, "Right Text (TMP)", "{action:NextPanel}");

            var uiRect = new CanvasUtil.RectData(Vector2.zero, Vector2.zero, new Vector2(0.065f, 0.120f), new Vector2(0.935f, 0.843f));
            var uiArea = CanvasUtil.CreateBasePanel(panel, uiRect);
            uiArea.name = "WarpUI";
            uiArea.AddComponent<Canvas>();

            var headerRect = new CanvasUtil.RectData(Vector2.zero, 
                                                     Vector2.zero, 
                                                     new Vector2(0.02f, 0.87f), 
                                                     new Vector2(1, 1f));
            var headerText = CanvasUtil.CreateTextPanel(uiArea, "_WARP_HEADER", 10, TextAnchor.MiddleLeft, headerRect, CanvasUtil.GameFont);
            headerText.name = "Header";
            headerText.AddComponent<TranslateText>();

            //Update indicators to reference Warp on adjacent screens
            var mapPanel = mapCanvas.transform.GetChild(0).gameObject;
            SetText(mapPanel, "Left Panel Title Text", "_WARP");

            var chipsCanvas = self.transform.Find("ChipsCanvas").gameObject;
            var chipsPanel = chipsCanvas.transform.GetChild(0).gameObject;
            SetText(chipsPanel, "Right Panel Title Text", "_WARP");

            var allChipsCanvas = self.transform.Find("AllChipsCanvas").gameObject;
            var allChipsPanel = allChipsCanvas.transform.GetChild(0).gameObject;
            SetText(allChipsPanel, "Right Panel Title Text", "_WARP");

            var mainCursor = self.transform.Find("HUDCanvas/Panel/UICursor").gameObject;
            var newCursor = Instantiate(mainCursor, uiArea.transform);
            var newRect = newCursor.GetComponent<RectTransform>();
            newRect.offsetMin = Vector2.zero;
            newRect.offsetMax = Vector2.zero;
            newRect.sizeDelta = new Vector2(0.01f, 0f);
            newCursor.SetActive(true);

            var warp = uiArea.AddComponent<RepairStationWarp>();
            warp.selectedIndicator = newCursor.GetComponent<RectTransform>();

            var wake = AddStation(warp, uiArea, 10, "_SAVE_AREA_WAKE", "Haiku Wake Location", 0, 0);
            wake.isHaikuWake = true;
            AddStation(warp, uiArea, 10, "_SAVE_AREA_10", "Abandoned Wastes", 0, 1);
            AddStation(warp, uiArea, 15, "_SAVE_AREA_15", "Before Magnet", 0, 2);
            AddStation(warp, uiArea, 71, "_SAVE_AREA_71", "Car Battery", 0, 3);
            AddStation(warp, uiArea, 57, "_SAVE_AREA_57", "Left Bunker", 0, 4);
            AddStation(warp, uiArea, 41, "_SAVE_AREA_41", "Mid Bunker", 0, 5);
            AddStation(warp, uiArea, 218, "_SAVE_AREA_218", "Furnace", 0, 6);
            AddStation(warp, uiArea, 195, "_SAVE_AREA_195", "Right Factory", 0, 7);
            AddStation(warp, uiArea, 172, "_SAVE_AREA_172", "Mid Factory", 0, 8);
            AddStation(warp, uiArea, 194, "_SAVE_AREA_194", "Left Factory", 1, 0);
            AddStation(warp, uiArea, 87, "_SAVE_AREA_87", "Pinions", 1, 1);

            //var texWake = TextureUtils.LoadEmbedded("HaikuWake.png", 900, 900);
            //var sprite = Sprite.Create(texWake, new Rect(0, 0, 900, 900), Vector2.zero);
            //var imgPanel = CanvasUtil.CreateImagePanel(uiArea, sprite,
            //                                           new CanvasUtil.RectData(
            //                                               Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f),
            //                                               new Vector2(0.98f, 0.98f)));
        }

        private static void SetTextTmp(GameObject panel, string objName, string text)
        {
            var textObj = panel.transform.Find(objName);
            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
        }

        private static void SetText(GameObject panel, string objName, string text)
        {
            var textObj = panel.transform.Find(objName);
            var tmp = textObj.GetComponent<Text>();
            tmp.text = text;
        }

        private const float stationX = 0.03f;
        private const float stationY = 0.75f;
        private const float stationWidth = 0.23f;
        private const float stationPitch = 0.25f;
        private const float stationHeight = 0.09f;

        private static RepairStationWarpButton AddStation(RepairStationWarp owner, GameObject uiArea, int sceneId, string key, string text, int ix, int iy)
        {
            LocalizationSystem.localizedEN.Add(key, text);

            var rect = new CanvasUtil.RectData(Vector2.zero,
                                               Vector2.zero,
                                               new Vector2(stationX + ix*stationPitch, stationY - iy*stationHeight),
                                               new Vector2(stationX + ix*stationPitch + stationWidth, stationY - iy*stationHeight + stationHeight));
            var textObj = CanvasUtil.CreateTextPanel(uiArea, key, 8, TextAnchor.MiddleLeft, rect, CanvasUtil.GameFont);
            textObj.name = "Station " + key;
            textObj.AddComponent<TranslateText>();
            textObj.AddComponent<Button>();
            var warpButton = textObj.AddComponent<RepairStationWarpButton>();
            warpButton.owner = owner;
            warpButton.sceneId = sceneId;

            return warpButton;
        }
    }
}
