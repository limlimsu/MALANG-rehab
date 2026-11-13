using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Malang.EditorTools
{
    /// <summary>
    /// 공용 UI 스프라이트를 코드로 만든다(외부 에셋 없이).
    /// - ui_round.png  : 둥근 사각 9-slice (모든 버튼·카드 공용). Image.pixelsPerUnitMultiplier 로 모서리 반경 조절
    /// - ui_circle.png : 원 (언덕, 캐릭터 몸, 점)
    /// - ui_ring.png   : 고리 (진행 링, 자물쇠 고리, 입)
    /// - ui_stripes.png: 대각선 빗금 타일 (게이지 목표 구간)
    /// - ui_dash.png   : 가로 점선 타일 (구간 경계, 1단계 기준선)
    /// - ui_triangle.png: 위를 향한 이등변 삼각형 (로켓 코)
    /// </summary>
    public static class SpriteBuilder
    {
        public const string Folder = "Assets/_Project/Art/Sprites";
        public const string RoundPath = Folder + "/ui_round.png";
        public const string CirclePath = Folder + "/ui_circle.png";
        public const string RingPath = Folder + "/ui_ring.png";
        public const string StripesPath = Folder + "/ui_stripes.png";
        public const string DashPath = Folder + "/ui_dash.png";
        public const string TrianglePath = Folder + "/ui_triangle.png";

        /// <summary>ui_round.png 의 모서리 반경(px). pixelsPerUnitMultiplier = RoundRadius / 원하는 반경.</summary>
        public const int RoundRadius = 48;

        private const int RoundSize = 128;
        private const int CircleSize = 256;
        private const float RingThickness = 0.156f; // 지름 대비 두께

        private const int StripeSize = 24;          // 빗금 주기(대각선 방향 픽셀 합)
        private const float StripeAlphaStrong = 0.28f;
        private const float StripeAlphaWeak = 0.10f;
        private const int DashWidth = 24;
        private const int DashHeight = 6;
        private const int DashOn = 14;
        private const int TriangleSize = 128;

        [MenuItem("MALANG/Rebuild UI Sprites", priority = 20)]
        public static void Rebuild()
        {
            Ensure(true);
        }

        public static void Ensure(bool force = false)
        {
            SceneBuilder.EnsureFolder(Folder);

            if (force || !File.Exists(RoundPath))
                Write(RoundPath, RoundSize, RoundSize, RoundRectCoverage);
            if (force || !File.Exists(CirclePath))
                Write(CirclePath, CircleSize, CircleSize, CircleCoverage);
            if (force || !File.Exists(RingPath))
                Write(RingPath, CircleSize, CircleSize, RingCoverage);
            if (force || !File.Exists(StripesPath))
                Write(StripesPath, StripeSize, StripeSize, StripeCoverage);
            if (force || !File.Exists(DashPath))
                Write(DashPath, DashWidth, DashHeight, DashCoverage);
            if (force || !File.Exists(TrianglePath))
                Write(TrianglePath, TriangleSize, TriangleSize, TriangleCoverage);

            Configure(RoundPath, new Vector4(RoundRadius, RoundRadius, RoundRadius, RoundRadius), TextureWrapMode.Clamp);
            Configure(CirclePath, Vector4.zero, TextureWrapMode.Clamp);
            Configure(RingPath, Vector4.zero, TextureWrapMode.Clamp);
            Configure(StripesPath, Vector4.zero, TextureWrapMode.Repeat);
            Configure(DashPath, Vector4.zero, TextureWrapMode.Repeat);
            Configure(TrianglePath, Vector4.zero, TextureWrapMode.Clamp);
        }

        public static Sprite Load(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Write(string path, int width, int height, Func<float, float, int, int, float> coverage)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = Mathf.Clamp01(coverage(x + 0.5f, y + 0.5f, width, height));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void Configure(string path, Vector4 border, TextureWrapMode wrap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteBorder != border ||
                           importer.mipmapEnabled ||
                           importer.wrapMode != wrap ||
                           settings.spriteMeshType != SpriteMeshType.FullRect ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;

            if (!changed)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // Tiled 이미지에 필요
            importer.SetTextureSettings(settings);

            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static float RoundRectCoverage(float px, float py, int w, int h)
        {
            float half = w * 0.5f;
            float qx = Mathf.Max(Mathf.Abs(px - half) - (half - RoundRadius), 0f);
            float qy = Mathf.Max(Mathf.Abs(py - half) - (half - RoundRadius), 0f);
            float d = Mathf.Sqrt(qx * qx + qy * qy) - RoundRadius;
            return 0.5f - d;
        }

        private static float CircleCoverage(float px, float py, int w, int h)
        {
            float half = w * 0.5f;
            float d = Vector2.Distance(new Vector2(px, py), new Vector2(half, half)) - (half - 1f);
            return 0.5f - d;
        }

        private static float RingCoverage(float px, float py, int w, int h)
        {
            float half = w * 0.5f;
            float outer = half - 1f;
            float inner = outer - w * RingThickness;
            float r = Vector2.Distance(new Vector2(px, py), new Vector2(half, half));
            return Mathf.Min(0.5f - (r - outer), 0.5f - (inner - r));
        }

        /// <summary>시안: repeating-linear-gradient(135deg, .28 / .10) — 이음매 없이 반복되는 대각선 띠.</summary>
        private static float StripeCoverage(float px, float py, int w, int h)
        {
            float t = Mathf.Repeat(px + py, StripeSize);
            return t < StripeSize * 0.5f ? StripeAlphaStrong : StripeAlphaWeak;
        }

        private static float DashCoverage(float px, float py, int w, int h)
        {
            return px < DashOn ? 1f : 0f;
        }

        /// <summary>꼭짓점 (가운데 위), (왼쪽 아래), (오른쪽 아래). 세 변까지의 부호 거리 중 최소값으로 가장자리 부드럽게.</summary>
        private static float TriangleCoverage(float px, float py, int w, int h)
        {
            var a = new Vector2(w * 0.5f, h - 1f);
            var b = new Vector2(1f, 1f);
            var c = new Vector2(w - 1f, 1f);
            var p = new Vector2(px, py);
            float d = Mathf.Min(EdgeDistance(p, a, b), Mathf.Min(EdgeDistance(p, b, c), EdgeDistance(p, c, a)));
            return d + 0.5f;
        }

        /// <summary>선분 from→to 의 왼쪽(삼각형 안쪽, 반시계 순서)이 양수인 거리.</summary>
        private static float EdgeDistance(Vector2 p, Vector2 from, Vector2 to)
        {
            Vector2 e = to - from;
            Vector2 n = new Vector2(-e.y, e.x).normalized;
            return Vector2.Dot(p - from, n);
        }
    }
}
