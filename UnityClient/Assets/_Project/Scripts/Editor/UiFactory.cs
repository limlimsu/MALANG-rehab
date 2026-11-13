using Malang.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.EditorTools
{
    /// <summary>SceneBuilder 가 쓰는 uGUI 생성 도우미. 좌표·크기는 기준 해상도 1920×1200 px.</summary>
    public static class UiFactory
    {
        public static Sprite Round;
        public static Sprite Circle;
        public static Sprite Ring;
        public static Sprite Stripes;
        public static Sprite Dash;
        public static Sprite Triangle;

        private const int UiLayer = 5;
        private const float ButtonShadowOffset = 13f;

        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);

        public class Pill
        {
            public RectTransform Rect;
            public Image Background;
            public LocText Label;
        }

        public static void LoadSprites()
        {
            Round = SpriteBuilder.Load(SpriteBuilder.RoundPath);
            Circle = SpriteBuilder.Load(SpriteBuilder.CirclePath);
            Ring = SpriteBuilder.Load(SpriteBuilder.RingPath);
            Stripes = SpriteBuilder.Load(SpriteBuilder.StripesPath);
            Dash = SpriteBuilder.Load(SpriteBuilder.DashPath);
            Triangle = SpriteBuilder.Load(SpriteBuilder.TrianglePath);
        }

        /// <summary>반복 무늬 이미지 (빗금, 점선).</summary>
        public static Image TiledImg(Transform parent, string name, Color color, Sprite sprite)
        {
            Image image = Img(parent, name, color, sprite);
            image.type = Image.Type.Tiled;
            return image;
        }

        // ---------- RectTransform ----------

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f,
            float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Place(Component c, Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size)
        {
            return Place((RectTransform)c.transform, anchor, pivot, position, size);
        }

        // ---------- Image ----------

        public static Image Img(Transform parent, string name, Color color, Sprite sprite = null)
        {
            RectTransform rt = Node(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>둥근 사각 9-slice 이미지. radius = 모서리 반경(px).</summary>
        public static Image RoundImg(Transform parent, string name, Color color, float radius)
        {
            Image image = Img(parent, name, color, Round);
            SetRadius(image, radius);
            return image;
        }

        public static void SetRadius(Image image, float radius)
        {
            image.sprite = Round;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = SpriteBuilder.RoundRadius / Mathf.Max(1f, radius);
        }

        /// <summary>
        /// 원형 채움 이미지. origin: 0 아래, 1 오른쪽, 2 위, 3 왼쪽 (시계 방향 채움).
        /// </summary>
        public static Image RadialImg(Transform parent, string name, Color color, Sprite sprite, int origin,
            float amount)
        {
            Image image = Img(parent, name, color, sprite);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = origin;
            image.fillClockwise = true;
            image.fillAmount = amount;
            return image;
        }

        // ---------- Text ----------

        public static LocText Txt(Transform parent, string name, string key, FontRole role, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rt = Node(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            var loc = rt.gameObject.AddComponent<LocText>();
            loc.Setup(key, role);
            return loc;
        }

        // ---------- Pill / Button ----------

        /// <summary>
        /// 글자 폭에 맞춰 늘어나는 둥근 알약 모양. fitWidth=false 면 부모 레이아웃 그룹이 폭을 정한다.
        /// </summary>
        public static Pill MakePill(Transform parent, string name, Color background, float height, float padX,
            float radius, string key, FontRole role, float fontSize, Color textColor, bool fitWidth = true)
        {
            Image bg = RoundImg(parent, name, background, radius);
            RectTransform rt = bg.rectTransform;
            rt.sizeDelta = new Vector2(padX * 2f, height);

            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(Mathf.RoundToInt(padX), Mathf.RoundToInt(padX), 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            if (fitWidth)
            {
                var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            LocText label = Txt(rt, "Label", key, role, fontSize, textColor);
            return new Pill { Rect = rt, Background = bg, Label = label };
        }

        public static Button MakeButton(Pill pill, bool withShadow, Color shadowColor)
        {
            pill.Background.raycastTarget = true;
            var button = pill.Rect.gameObject.AddComponent<Button>();
            button.targetGraphic = pill.Background;

            if (withShadow)
            {
                var shadow = pill.Rect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = shadowColor;
                shadow.effectDistance = new Vector2(0f, -ButtonShadowOffset);
                shadow.useGraphicAlpha = true;
            }

            return button;
        }

        public static Button Btn(Transform parent, string name, Color background, Color shadow, float height,
            float padX, string key, FontRole role, float fontSize, Color textColor, out Pill pill)
        {
            pill = MakePill(parent, name, background, height, padX, height * 0.5f, key, role, fontSize,
                textColor);
            return MakeButton(pill, true, shadow);
        }

        public static void IgnoreLayout(Component c)
        {
            var element = c.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        public static void FixedLayoutSize(Component c, float width, float height)
        {
            var element = c.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = height;
            element.preferredHeight = height;
        }

        // ---------- Serialized wiring ----------

        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("SceneBuilder: " + target.GetType().Name + " 에 필드가 없습니다: " + field);
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray)
            {
                Debug.LogError("SceneBuilder: " + target.GetType().Name + " 에 배열 필드가 없습니다: " + field);
                return;
            }

            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
