using System.Collections.Generic;
using Malang.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Malang.EditorTools
{
    /// <summary>
    /// Art/Fonts 의 TTF 4종으로 Dynamic TMP 폰트 에셋을 만들고 GameConfig 에 연결한다.
    /// 영어 폰트 Fallback 에 한글 폰트를 넣는다 (Fredoka → Jua, Noto Sans → Gowun Dodum).
    /// </summary>
    public static class FontBuilder
    {
        private const string SourceFolder = "Assets/_Project/Art/Fonts";
        private const string OutputFolder = SourceFolder + "/TMP";

        private const int SamplingPointSize = 72;
        private const int AtlasPadding = 7;  // 샘플 크기의 약 10%
        private const int AtlasSize = 2048;

        [MenuItem("MALANG/Rebuild Fonts", priority = 21)]
        public static void RebuildMenu()
        {
            GameConfig config = SceneBuilder.EnsureConfig();
            Ensure(config);
            AssetDatabase.SaveAssets();
            Debug.Log("MALANG: 폰트 에셋 확인 완료 (" + OutputFolder + ")");
        }

        public static void Ensure(GameConfig config)
        {
            SceneBuilder.EnsureFolder(OutputFolder);

            TMP_FontAsset koTitle = GetOrCreate("Jua-Regular.ttf", "Jua SDF");
            TMP_FontAsset koBody = GetOrCreate("GowunDodum-Regular.ttf", "GowunDodum SDF");
            TMP_FontAsset enTitle = GetOrCreate("Fredoka-SemiBold.ttf", "Fredoka SDF");
            TMP_FontAsset enBody = GetOrCreate("NotoSans-Regular.ttf", "NotoSans SDF");

            SetFallbacks(enTitle, koTitle, koBody);
            SetFallbacks(enBody, koBody, koTitle);
            // 순환 참조를 만들지 않는다: Jua 에 없는 기호는 Gowun Dodum 에서 찾는다.
            SetFallbacks(koTitle, koBody);
            SetFallbacks(koBody);

            config.koTitleFont = koTitle;
            config.koBodyFont = koBody;
            config.enTitleFont = enTitle;
            config.enBodyFont = enBody;
            EditorUtility.SetDirty(config);
        }

        private static TMP_FontAsset GetOrCreate(string fileName, string assetName)
        {
            string assetPath = OutputFolder + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;

            string fontPath = SourceFolder + "/" + fileName;
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError("FontBuilder: 폰트 파일이 없습니다: " + fontPath);
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);

            if (asset == null)
            {
                Debug.LogError("FontBuilder: TMP 폰트 에셋 생성 실패: " + fontPath);
                return null;
            }

            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, assetPath);

            // 아틀라스 텍스처와 머티리얼을 폰트 에셋의 하위 에셋으로 저장한다.
            if (asset.atlasTexture != null)
            {
                asset.atlasTexture.name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            }

            if (asset.material != null)
            {
                asset.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }

        private static void SetFallbacks(TMP_FontAsset target, params TMP_FontAsset[] fallbacks)
        {
            if (target == null)
                return;

            var list = new List<TMP_FontAsset>();
            for (int i = 0; i < fallbacks.Length; i++)
            {
                if (fallbacks[i] != null && fallbacks[i] != target)
                    list.Add(fallbacks[i]);
            }

            target.fallbackFontAssetTable = list;
            EditorUtility.SetDirty(target);
        }
    }
}
