using UnityEngine;

namespace Malang.UI
{
    /// <summary>비주얼 가이드(SPEC §14) 색 토큰.</summary>
    public static class Palette
    {
        public static readonly Color Sky = new Color32(0xDC, 0xEB, 0xFF, 0xFF);
        public static readonly Color Hill1 = new Color32(0xBF, 0xE9, 0xD2, 0xFF);
        public static readonly Color Hill2 = new Color32(0xA9, 0xE2, 0xC4, 0xFF);
        public static readonly Color Navy = new Color32(0x26, 0x2A, 0x63, 0xFF);
        public static readonly Color Yellow = new Color32(0xFF, 0xCF, 0x3A, 0xFF);
        public static readonly Color YellowShadow = new Color32(0xE3, 0xA8, 0x00, 0xFF);
        public static readonly Color Green = new Color32(0x3F, 0xCF, 0x8E, 0xFF);
        public static readonly Color GreenDeep = new Color32(0x1E, 0x8F, 0x5E, 0xFF);
        public static readonly Color Purple = new Color32(0x9B, 0x8C, 0xFF, 0xFF);
        public static readonly Color PurpleDeep = new Color32(0x7C, 0x6B, 0xEA, 0xFF);
        public static readonly Color Pink = new Color32(0xFF, 0x86, 0xAE, 0xFF);
        public static readonly Color PinkDeep = new Color32(0xF0, 0x64, 0x8F, 0xFF);
        public static readonly Color White = Color.white;

        /// <summary>힘을 뺀 상태(쉬기 구간) — 게이지 가독성 보강용 추가 토큰.</summary>
        public static readonly Color RelaxBlue = new Color32(0x7F, 0xC4, 0xFF, 0xFF);

        // 시안에서 쓰인 보조 색
        public static readonly Color ButtonText = new Color32(0x5A, 0x3E, 0x00, 0xFF);
        public static readonly Color LevelText = new Color32(0x6B, 0x4A, 0x00, 0xFF);
        public static readonly Color SubText = new Color32(0x4A, 0x50, 0x80, 0xFF);
        public static readonly Color DisabledText = new Color32(0x9A, 0xA0, 0xC4, 0xFF);
        public static readonly Color DisabledShadow = new Color32(0xC9, 0xD2, 0xEA, 0xFF);
        public static readonly Color WhiteTranslucent = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color WhiteSoft = new Color(1f, 1f, 1f, 0.75f);
        public static readonly Color Dim = new Color(0x26 / 255f, 0x2A / 255f, 0x63 / 255f, 0.55f);
    }
}
