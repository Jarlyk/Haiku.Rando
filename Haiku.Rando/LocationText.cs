using UnityEngine.SceneManagement;

namespace Haiku.Rando
{
    public class LocationText
    {
        public string Where;
        public bool ShowInCornerPopup;

        internal static LocationText OfCurrentScene()
        {
            var s = SceneManager.GetActiveScene();
            if (s == null)
            {
                return null;
            }
            var n = s.buildIndex;
            if (n >= 0 && n < areaNamesByScene.Length)
            {
                var code = areaNamesByScene[n];
                return new()
                {
                    Where = LocalizationSystem.GetLocalizedValue("_RANDO_AREA_" + code),
                    ShowInCornerPopup = false
                };
            }
            return null;
        }

        private const string AW = "AW";
        private const string LB = "LB";
        private const string CC = "CC";
        private const string PE = "PE";
        private const string IB = "IB";
        private const string WD = "WD";
        private const string FR = "FR";
        private const string SW = "SW";
        private const string RL = "RL";
        private const string FF = "FF";
        private const string RS = "RS";
        private const string BF = "BF";
        private const string OA = "OA";
        
        private static readonly string[] areaNamesByScene = new string[]
        {
            /* 000 */ "", "", "", "", "", "", "", "", "", "",
            /* 010 */ AW, AW, AW, AW, AW, AW, AW, AW, AW, AW,
            /* 020 */ AW, AW, AW, AW, AW, AW, AW, AW, AW, AW,
            /* 030 */ LB, LB, LB, LB, LB, LB, LB, LB, LB, LB,
            /* 040 */ LB, LB, LB, LB, LB, LB, LB, LB, LB, LB,
            /* 050 */ LB, LB, LB, LB, LB, LB, LB, LB, LB, LB,
            /* 060 */ LB, CC, CC, CC, CC, CC, CC, CC, CC, CC,
            /* 070 */ CC, CC, CC, CC, CC, CC, CC, CC, CC, CC,
            /* 080 */ CC, CC, CC, CC, CC, CC, PE, PE, PE, PE,
            /* 090 */ PE, PE, PE, PE, PE, PE, PE, PE, PE, PE,
            /* 100 */ PE, IB, IB, IB, IB, IB, IB, IB, IB, IB,
            /* 110 */ IB, WD, WD, WD, WD, WD, WD, WD, WD, WD,
            /* 120 */ WD, WD, WD, WD, WD, WD, WD, WD, WD, WD,
            /* 130 */ WD, FR, FR, FR, FR, FR, FR, FR, FR, FR,
            /* 140 */ WD, FR, "", FR, FR, FR, SW, FR, "", "",
            /* 150 */ "", SW, SW, SW, SW, SW, SW, SW, SW, RL,
            /* 160 */ SW, SW, SW, RL, RL, SW, AW, CC, CC, CC,
            /* 170 */ CC, FF, FF, FF, FF, FF, FF, FF, FF, FF,
            /* 180 */ FF, FF, FF, FF, FF, FF, FF, FF, FF, FF,
            /* 190 */ FF, FF, FF, FF, FF, FF, RS, RS, RS, FF,
            /* 200 */ BF, LB, CC, LB, LB, "", WD, WD, WD, WD,
            /* 210 */ WD, FR, FF, BF, BF, BF, BF, BF, BF, "",
            /* 220 */ CC, FF, LB, WD, RS, FR, "", FR, CC, FF,
            /* 230 */ FF, FF, BF, FF, LB, "", "", "", "", "",
            /* 240 */ "", "", "", "", "", "", OA, OA, OA, OA,
            /* 250 */ OA, OA, OA, OA, OA, OA, OA, OA, OA, OA,
            /* 260 */ AW, "", "", "", "", "", "", "", "", OA,
            /* 270 */ OA, OA
        };
    }
}
