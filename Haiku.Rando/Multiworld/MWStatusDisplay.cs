using MAPI = Modding;
using UE = UnityEngine;
using UEUI = UnityEngine.UI;

namespace Haiku.Rando.Multiworld
{
    internal class MWStatusDisplay : UE.MonoBehaviour
    {
        private UE.GameObject _canvas;
        private UEUI.Text _canvasText;

        public void Start()
        {
            _canvas = MAPI.CanvasUtil.CreateCanvas(1);
            _canvas.name = "MWStatusCanvas";
            _canvas.transform.SetParent(gameObject.transform);

            var rect = new MAPI.CanvasUtil.RectData(
                new(250, 25), new(50, 100)
            );
            var panel = MAPI.CanvasUtil.CreateTextPanel(_canvas, "", 9, UE.TextAnchor.MiddleRight, rect, MAPI.CanvasUtil.GameFont);
            _canvasText = panel.GetComponent<UEUI.Text>();
        }

        public string Text
        {
            get { return _canvasText.text; }
            set
            {
                _canvas.SetActive(value != "");
                _canvasText.text = value;
            }
        }
    }
}