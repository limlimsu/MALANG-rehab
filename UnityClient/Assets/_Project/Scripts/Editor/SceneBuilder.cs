using Malang.Core;
using Malang.Input;
using Malang.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Malang.EditorTools.UiFactory;

namespace Malang.EditorTools
{
    /// <summary>
    /// MALANG > Build Scene : Main.unity 를 처음부터 다시 만든다.
    /// Canvas·화면 패널·버튼·텍스트 계층과 컴포넌트 참조를 모두 코드로 연결한다.
    /// 화면을 손으로 고치지 말고 이 파일을 고친 뒤 다시 실행할 것.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        /// <summary>씬 구성 버전. 값은 AppManager.ExpectedSceneVersion 에서 올린다 (실행 중 확인에도 쓰임).</summary>
        public const int SceneVersion = AppManager.ExpectedSceneVersion;
        public const string ConfigAssetPath = "Assets/_Project/Resources/Config/GameConfig.asset";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1200f);
        private const float MatchWidthOrHeight = 0.5f;

        // 레이아웃 (기준 해상도 px, 시안의 cqw × 19.2)
        private const float Margin = 50f;          // 좌우 여백 2.6cqw
        private const float TopY = 38f;            // 상단 여백 2cqw
        private const float BottomY = 58f;         // 하단 버튼 여백
        private const float GaugeWidth = 123f;     // 6.4cqw
        private const float GaugeGap = 38f;
        private const float PlayTop = 150f;        // 상단 바 아래
        private const float PlayBottom = 170f;     // "다음" 버튼 위

        [MenuItem("MALANG/Build Scene", priority = 0)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("MALANG: Play 모드에서는 Build Scene 을 실행할 수 없습니다.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder("Assets/_Project/Scenes");
            GameConfig config = EnsureConfig();
            SpriteBuilder.Ensure();
            FontBuilder.Ensure(config);
            AssetDatabase.SaveAssets();
            LoadSprites();

            GameConfig.ClearCache();
            Loc.SetLanguage(config.defaultLanguage);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();
            RectTransform canvas = CreateCanvas();
            BuildBackground(canvas);

            var app = new GameObject("App");
            var appManager = app.AddComponent<AppManager>();
            var router = app.AddComponent<ScreenRouter>();
            Set(appManager, "router", router);
            var appSo = new SerializedObject(appManager);
            appSo.FindProperty("sceneVersion").intValue = SceneVersion;
            appSo.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("GripSource").AddComponent<GripSource>();

            // 그리는 순서(뒤 → 앞): 배경, 화면, 세기 버튼, 상태 표시, 보호자 모드, 디버그
            RectTransform screens = Stretch(Node("Screens", canvas));
            BuildMockPad(canvas);
            BuildInputStatus(canvas);
            GuardianPanel guardian = BuildGuardian(canvas, router);
            DebugOverlay debug = BuildDebugOverlay(canvas);
            Set(appManager, "debugOverlay", debug);

            ScreenBase[] all =
            {
                BuildBoot(screens),
                BuildDeviceSelect(screens),
                BuildCalibration(screens),
                BuildMain(screens, guardian, config),
                BuildStage1(screens, config),
                BuildStage2(screens),
                BuildStage3(screens),
                BuildResult(screens)
            };

            SetArray(router, "screens", all);
            for (int i = 0; i < all.Length; i++)
                all[i].gameObject.SetActive(false);
            guardian.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("MALANG: 씬 생성 완료 → " + ScenePath);
        }

        // ---------- 에셋 준비 ----------

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        public static GameConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            if (config != null)
                return config;

            EnsureFolder("Assets/_Project/Resources/Config");
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("MALANG: GameConfig 생성 → " + ConfigAssetPath);
            return config;
        }

        // ---------- 공통 오브젝트 ----------

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Sky;
            cam.orthographic = true;
            go.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.layer = 5;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        private static void BuildBackground(RectTransform canvas)
        {
            RectTransform bg = Stretch(Node("Background", canvas));
            Stretch(Img(bg, "Sky", Palette.Sky).rectTransform);

            // 시안: hill a 80%×40%, left -15%, bottom -24% / hill b 70%×36%, right -12%, bottom -22%
            Place(Img(bg, "HillA", Palette.Hill1, Circle), BottomLeft, BottomLeft,
                new Vector2(-288f, -288f), new Vector2(1536f, 480f));
            Place(Img(bg, "HillB", Palette.Hill2, Circle), BottomRight, BottomRight,
                new Vector2(230f, -264f), new Vector2(1344f, 432f));
        }

        private static RectTransform ScreenRoot(string name, Transform parent)
        {
            return Stretch(Node(name, parent));
        }

        /// <summary>캐릭터 "말랑이" 자리표시(시안 SVG 비율). Squish 자식은 M2 에서 눌림 애니메이션 대상.</summary>
        private static RectTransform BuildCharacter(Transform parent, string name, Vector2 position, float width)
        {
            float s = width / 200f; // 시안 viewBox 200×190
            RectTransform root = Place(Node(name, parent), Center, Center, position,
                new Vector2(width, 190f * s));

            // 눌림 기준점은 몸 아래쪽 (시안 transform-origin 100px 168px)
            RectTransform squish = Node("Squish", root);
            squish.anchorMin = squish.anchorMax = new Vector2(0.5f, 22f / 190f);
            squish.pivot = Center;
            squish.anchoredPosition = Vector2.zero;
            squish.sizeDelta = Vector2.zero;

            // 시안 좌표(cx, cy) → Squish 기준 위치
            System.Func<float, float, Vector2> P = (cx, cy) => new Vector2((cx - 100f) * s, (168f - cy) * s);

            Place(Img(squish, "Body", Palette.Pink, Circle), Center, Center, P(100f, 107f),
                new Vector2(160f * s, 140f * s));

            Image shine = Img(squish, "Shine", new Color(1f, 1f, 1f, 0.45f), Circle);
            Place(shine, Center, Center, P(70f, 72f), new Vector2(36f * s, 20f * s));
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f);

            Color cheek = Palette.PinkDeep;
            cheek.a = 0.55f;
            Place(Img(squish, "CheekL", cheek, Circle), Center, Center, P(58f, 128f), new Vector2(26f * s, 16f * s));
            Place(Img(squish, "CheekR", cheek, Circle), Center, Center, P(142f, 128f), new Vector2(26f * s, 16f * s));

            RectTransform eyes = Stretch(Node("Eyes", squish));
            eyes.anchorMin = eyes.anchorMax = Center;
            eyes.sizeDelta = Vector2.zero;
            Place(Img(eyes, "EyeL", Palette.Navy, Circle), Center, Center, P(76f, 108f), new Vector2(20f * s, 20f * s));
            Place(Img(eyes, "EyeR", Palette.Navy, Circle), Center, Center, P(124f, 108f), new Vector2(20f * s, 20f * s));
            Place(Img(eyes, "SparkL", Palette.White, Circle), Center, Center, P(79f, 104f), new Vector2(6f * s, 6f * s));
            Place(Img(eyes, "SparkR", Palette.White, Circle), Center, Center, P(127f, 104f), new Vector2(6f * s, 6f * s));

            Place(RadialImg(squish, "Mouth", Palette.Navy, Ring, 1, 0.5f), Center, Center, P(100f, 130f),
                new Vector2(22f * s, 18f * s));

            return root;
        }

        /// <summary>힘에 따라 눌리는 캐릭터.</summary>
        private static CharacterView AddSquish(RectTransform character)
        {
            var view = character.gameObject.AddComponent<CharacterView>();
            Set(view, "squish", character.Find("Squish"));
            return view;
        }

        // ---------- 화면: 부트 ----------

        private static ScreenBase BuildBoot(Transform parent)
        {
            RectTransform root = ScreenRoot("Boot", parent);
            var screen = root.gameObject.AddComponent<BootScreen>();

            BuildCharacter(root, "Character", new Vector2(0f, 170f), 340f);
            Place(Txt(root, "Logo", "char.name", FontRole.Title, 110f, Palette.Navy), Center, Center,
                new Vector2(0f, -70f), new Vector2(1200f, 130f));
            Place(Txt(root, "Connecting", "boot.connecting", FontRole.Body, 40f, Palette.SubText), Center,
                Center, new Vector2(0f, -170f), new Vector2(1200f, 60f));

            return screen;
        }

        // ---------- 화면: 보정 ----------

        private static ScreenBase BuildCalibration(Transform parent)
        {
            RectTransform root = ScreenRoot("Calibration", parent);
            var screen = root.gameObject.AddComponent<CalibrationScreen>();

            Pill chip = MakePill(root, "TitleChip", Palette.White, 76f, 35f, 38f, "cal.title", FontRole.Title,
                40f, Palette.Navy);
            Place(chip.Rect, TopLeft, TopLeft, new Vector2(Margin, -TopY), chip.Rect.sizeDelta);

            AddSquish(BuildCharacter(root, "Character", new Vector2(0f, 120f), 380f));
            Place(Txt(root, "Instruction", "cal.rest", FontRole.Title, 58f, Palette.Navy), Center, Center,
                new Vector2(0f, -150f), new Vector2(1600f, 80f));

            Pill pill;
            Button done = Btn(root, "DoneButton", Palette.Yellow, Palette.YellowShadow, 96f, 69f, "common.next",
                FontRole.Title, 44f, Palette.ButtonText, out pill);
            Place(pill.Rect, BottomRight, BottomRight, new Vector2(-Margin, BottomY), pill.Rect.sizeDelta);
            Set(screen, "doneButton", done);

            return screen;
        }

        // ---------- 화면: 메인 ----------

        private static ScreenBase BuildMain(Transform parent, GuardianPanel guardian, GameConfig config)
        {
            RectTransform root = ScreenRoot("Main", parent);
            var screen = root.gameObject.AddComponent<MainScreen>();

            // 레벨 원 (7cqw, 흰 테두리 .5cqw)
            const float badge = 134f;
            Image badgeRing = Img(root, "LevelBadge", Palette.White, Circle);
            Place(badgeRing, TopLeft, TopLeft, new Vector2(Margin, -TopY), new Vector2(badge, badge));
            Image badgeFill = Img(badgeRing.transform, "Fill", Palette.Yellow, Circle);
            Stretch(badgeFill.rectTransform, 10f, 10f, 10f, 10f);
            Place(Txt(badgeFill.transform, "Lv", "main.lv", FontRole.Title, 32f, Palette.LevelText), Center,
                Center, new Vector2(0f, 24f), new Vector2(110f, 36f));
            LocText levelNumber = Txt(badgeFill.transform, "Number", "", FontRole.Title, 52f, Palette.LevelText);
            Place(levelNumber, Center, Center, new Vector2(0f, -14f), new Vector2(110f, 56f));
            levelNumber.SetRaw(config.demoLevel.ToString());

            // 경험치 바 (42cqw × 2.6cqw)
            float xpX = Margin + badge + 31f;
            Image xpBar = RoundImg(root, "XpBar", Palette.White, 25f);
            Place(xpBar, TopLeft, TopLeft, new Vector2(xpX, -TopY - 22f), new Vector2(806f, 50f));
            Image xpFill = RoundImg(xpBar.transform, "Fill", Palette.Green, 19f);
            RectTransform fillRt = xpFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2((float)config.demoXp / config.XpNeededAt(config.demoLevel), 1f);
            fillRt.offsetMin = new Vector2(6f, 6f);
            fillRt.offsetMax = new Vector2(-6f, -6f);

            LocText toNext = Txt(root, "ToNext", "main.toNext", FontRole.Body, 32f, Palette.SubText,
                TextAlignmentOptions.Left);
            Place(toNext, TopLeft, TopLeft, new Vector2(xpX, -TopY - 82f), new Vector2(806f, 44f));
            toNext.SetArgs(config.XpNeededAt(config.demoLevel) - config.demoXp);

            // 자물쇠 (보호자 모드, 길게 누르기)
            Image lockBg = Img(root, "Lock", Palette.WhiteTranslucent, Circle);
            lockBg.raycastTarget = true;
            Place(lockBg, TopRight, TopRight, new Vector2(-Margin, -TopY - 19f), new Vector2(96f, 96f));
            Place(RoundImg(lockBg.transform, "Body", Palette.Navy, 6f), Center, Center, new Vector2(0f, -10f),
                new Vector2(40f, 30f));
            Place(RadialImg(lockBg.transform, "Shackle", Palette.Navy, Ring, 3, 0.5f), Center, Center,
                new Vector2(0f, 9f), new Vector2(30f, 30f));
            Place(Img(lockBg.transform, "LegL", Palette.Navy), Center, Center, new Vector2(-12.6f, 7f),
                new Vector2(4.7f, 6f));
            Place(Img(lockBg.transform, "LegR", Palette.Navy), Center, Center, new Vector2(12.6f, 7f),
                new Vector2(4.7f, 6f));
            Image progress = RadialImg(lockBg.transform, "HoldProgress", Palette.Green, Ring, 2, 0f);
            Place(progress, Center, Center, Vector2.zero, new Vector2(118f, 118f));
            progress.enabled = false;
            var lockButton = lockBg.gameObject.AddComponent<LongPressButton>();
            Set(lockButton, "progressRing", progress);

            // 캐릭터 + 이름 + 말풍선
            AddSquish(BuildCharacter(root, "Character", new Vector2(0f, 40f), 500f));
            Place(Txt(root, "Name", "char.name", FontRole.Title, 46f, Palette.Navy), Center, Center,
                new Vector2(0f, -225f), new Vector2(600f, 60f));

            Pill bubble = MakePill(root, "Bubble", Palette.White, 80f, 38f, 38f, "main.greet", FontRole.Body,
                36f, Palette.Navy);
            Place(bubble.Rect, Center, BottomLeft, new Vector2(154f, 230f), bubble.Rect.sizeDelta);
            Image tail = Img(bubble.Rect, "Tail", Palette.White);
            IgnoreLayout(tail);
            Place(tail, BottomLeft, Center, new Vector2(48f, 4f), new Vector2(30f, 30f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.transform.SetAsFirstSibling();
            Set(screen, "levelNumber", levelNumber);

            // 시작 버튼
            Pill startPill;
            Button start = Btn(root, "StartButton", Palette.Yellow, Palette.YellowShadow, 130f, 134f,
                "main.start", FontRole.Title, 58f, Palette.ButtonText, out startPill);
            Place(startPill.Rect, BottomCenter, BottomCenter, new Vector2(0f, BottomY + 13f),
                startPill.Rect.sizeDelta);

            Set(screen, "startButton", start);
            Set(screen, "lockButton", lockButton);
            Set(screen, "guardian", guardian);
            Set(screen, "xpFill", fillRt);
            Set(screen, "toNextText", toNext);
            return screen;
        }

        // ---------- 화면: 1–3단계 공통 ----------

        private class StageParts
        {
            public RectTransform Root;
            public RectTransform Arena;
            public LocText Counter;
            public Button Next;
            public Pill NextPill;
            public LocText Message;
            public ForceGauge Gauge;
        }

        /// <summary>
        /// 세로 힘 게이지 (6.4cqw 폭, 흰 테두리 .45cqw). 채움은 둥근 Mask 안에서 아래부터 찬다.
        /// 목표 구간 = 초록 빗금 + 위·아래 점선, 1단계 기준선 = 남색 점선 2개.
        /// </summary>
        private static ForceGauge BuildForceGauge(RectTransform screenRoot)
        {
            const float border = 9f;
            const float lineHeight = 6f;

            RectTransform root = Node("Gauge", screenRoot);
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 0.5f);
            root.offsetMin = new Vector2(-Margin - GaugeWidth, PlayBottom);
            root.offsetMax = new Vector2(-Margin, -PlayTop);

            Image track = RoundImg(root, "Track", Palette.White, GaugeWidth * 0.5f);
            Stretch(track.rectTransform, 0f, 60f, 0f, 0f);

            Image inner = RoundImg(track.transform, "Inner", Palette.White, GaugeWidth * 0.5f - border);
            Stretch(inner.rectTransform, border, border, border, border);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // 1단계 구간 면 (채움 뒤): 쥐기 구간 연한 노랑, 쉬기 구간 연한 하늘
            Color highTint = Palette.Yellow;
            highTint.a = 0.35f;
            Image zoneHigh = Img(inner.transform, "ZoneGrip", highTint);
            zoneHigh.gameObject.SetActive(false);
            Color lowTint = Palette.RelaxBlue;
            lowTint.a = 0.4f;
            Image zoneLow = Img(inner.transform, "ZoneRelax", lowTint);
            zoneLow.gameObject.SetActive(false);

            Image fill = Img(inner.transform, "Fill", Palette.Purple);
            RectTransform fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(1f, 0f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // 2단계 목표 구간 (채움 위에 겹쳐 그려서 채워진 뒤에도 보이게): 연한 초록 면 + 빗금 + 진한 점선 테두리
            RectTransform band = Node("Band", inner.transform);
            band.anchorMin = new Vector2(0f, 0.4f);
            band.anchorMax = new Vector2(1f, 0.6f);
            band.offsetMin = Vector2.zero;
            band.offsetMax = Vector2.zero;
            Color glowColor = Palette.Green;
            glowColor.a = 0.2f;
            Image bandGlow = Img(band, "Glow", glowColor);
            Stretch(bandGlow.rectTransform);
            Stretch(TiledImg(band, "Stripes", Palette.Green, Stripes).rectTransform);
            BandEdge(band, "EdgeTop", 1f, lineHeight + 2f);
            BandEdge(band, "EdgeBottom", 0f, lineHeight + 2f);
            band.gameObject.SetActive(false);

            // 1단계 기준선 2개
            Color markerColor = Palette.Navy;
            markerColor.a = 0.5f;
            var markers = new RectTransform[2];
            for (int i = 0; i < markers.Length; i++)
            {
                Image line = TiledImg(inner.transform, "Marker" + (i + 1), markerColor, Dash);
                markers[i] = line.rectTransform;
                markers[i].pivot = Center;
                markers[i].sizeDelta = new Vector2(0f, lineHeight);
                markers[i].gameObject.SetActive(false);
            }

            LocText value = Txt(root, "Value", "", FontRole.Title, 38f, Palette.Navy);
            RectTransform valueRt = value.Label.rectTransform;
            valueRt.anchorMin = new Vector2(0f, 0f);
            valueRt.anchorMax = new Vector2(1f, 0f);
            valueRt.pivot = new Vector2(0.5f, 0f);
            valueRt.anchoredPosition = Vector2.zero;
            valueRt.sizeDelta = new Vector2(40f, 50f);
            value.SetRaw("0%");

            // 목표 말풍선: 게이지 왼쪽, 게이지 안쪽과 같은 높이 범위에서 목표 위치(%)를 가리킨다.
            RectTransform lane = Node("CalloutLane", root);
            lane.anchorMin = new Vector2(0f, 0f);
            lane.anchorMax = new Vector2(0f, 1f);
            lane.pivot = new Vector2(1f, 0.5f);
            lane.offsetMin = new Vector2(-14f, 60f + border);
            lane.offsetMax = new Vector2(-14f, -border);

            Pill callout = MakePill(lane, "Callout", Palette.Yellow, 76f, 26f, 38f, "gauge.grip", FontRole.Title,
                40f, Palette.Navy);
            callout.Rect.pivot = new Vector2(1f, 0.5f);
            callout.Rect.anchorMin = callout.Rect.anchorMax = new Vector2(1f, 0.5f);
            callout.Rect.anchoredPosition = Vector2.zero;
            Image tail = Img(callout.Rect, "Tail", Palette.Yellow);
            IgnoreLayout(tail);
            Place(tail, new Vector2(1f, 0.5f), Center, new Vector2(0f, 0f), new Vector2(26f, 26f));
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.transform.SetAsFirstSibling();
            callout.Rect.gameObject.SetActive(false);

            var gauge = root.gameObject.AddComponent<ForceGauge>();
            Set(gauge, "fill", fillRt);
            Set(gauge, "fillImage", fill);
            Set(gauge, "band", band);
            Set(gauge, "bandGlow", bandGlow);
            Set(gauge, "zoneHigh", zoneHigh);
            Set(gauge, "zoneLow", zoneLow);
            SetArray(gauge, "markers", markers);
            Set(gauge, "valueText", value.Label);
            Set(gauge, "callout", callout.Rect);
            Set(gauge, "calloutBackground", callout.Background);
            Set(gauge, "calloutTail", tail);
            Set(gauge, "calloutLabel", callout.Label);
            return gauge;
        }

        private static void BandEdge(RectTransform band, string name, float y, float height)
        {
            RectTransform edge = TiledImg(band, name, Palette.GreenDeep, Dash).rectTransform;
            edge.anchorMin = new Vector2(0f, y);
            edge.anchorMax = new Vector2(1f, y);
            edge.pivot = new Vector2(0.5f, y);
            edge.anchoredPosition = Vector2.zero;
            edge.sizeDelta = new Vector2(0f, height);
        }

        private static StageParts BuildStageFrame(RectTransform root, string titleKey, int stageIndex,
            string counterKey, string nextKey)
        {
            var parts = new StageParts { Root = root };

            // 상단 왼쪽: 단계 칩 + 진행 점 3개
            RectTransform row = Node("TopLeft", root);
            Place(row, TopLeft, TopLeft, new Vector2(Margin, -TopY), new Vector2(0f, 76f));
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 27f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var rowFit = row.gameObject.AddComponent<ContentSizeFitter>();
            rowFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            Pill chip = MakePill(row, "Chip", Palette.White, 76f, 35f, 38f, titleKey, FontRole.Title, 40f,
                Palette.Navy, false);
            chip.Rect.sizeDelta = new Vector2(chip.Rect.sizeDelta.x, 76f);

            RectTransform dots = Node("Steps", row);
            dots.sizeDelta = new Vector2(0f, 27f);
            var dotLayout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotLayout.spacing = 15f;
            dotLayout.childAlignment = TextAnchor.MiddleLeft;
            dotLayout.childControlWidth = true;
            dotLayout.childControlHeight = true;
            dotLayout.childForceExpandWidth = false;
            dotLayout.childForceExpandHeight = false;
            for (int i = 0; i < 3; i++)
                BuildStepDot(dots, i, i < stageIndex ? 2 : (i == stageIndex ? 1 : 0));

            // 상단 오른쪽: 타이머/카운터 (남색 알약)
            Pill counter = MakePill(root, "Counter", Palette.Navy, 70f, 31f, 38f, counterKey, FontRole.Title,
                46f, Palette.White);
            Place(counter.Rect, TopRight, TopRight, new Vector2(-Margin, -TopY - 3f), counter.Rect.sizeDelta);
            parts.Counter = counter.Label;

            // 오른쪽 세로 힘 게이지
            parts.Gauge = BuildForceGauge(root);

            // 가운데 플레이 영역
            parts.Arena = Node("Arena", root);
            parts.Arena.anchorMin = Vector2.zero;
            parts.Arena.anchorMax = Vector2.one;
            parts.Arena.offsetMin = new Vector2(Margin, PlayBottom);
            parts.Arena.offsetMax = new Vector2(-(Margin + GaugeWidth + GaugeGap), -PlayTop);

            // 하단 오른쪽 "다음" 버튼
            Pill nextPill;
            parts.Next = Btn(root, "NextButton", Palette.Yellow, Palette.YellowShadow, 96f, 69f, nextKey,
                FontRole.Title, 44f, Palette.ButtonText, out nextPill);
            Place(nextPill.Rect, BottomRight, BottomRight, new Vector2(-Margin, BottomY), nextPill.Rect.sizeDelta);
            parts.Next.transition = Selectable.Transition.None; // 켜짐/꺼짐 색은 StageScreenBase 가 직접 바꾼다
            parts.NextPill = nextPill;

            return parts;
        }

        /// <summary>state: 0 아직, 1 지금, 2 완료</summary>
        private static void BuildStepDot(Transform parent, int index, int state)
        {
            Color color = state == 2 ? Palette.Green : Palette.Navy;
            Image dot = Img(parent, "Step" + (index + 1), color, Circle);
            FixedLayoutSize(dot, 27f, 27f);

            if (state == 0)
            {
                Stretch(Img(dot.transform, "Inner", Palette.White, Circle).rectTransform, 6f, 6f, 6f, 6f);
                dot.gameObject.AddComponent<CanvasGroup>().alpha = 0.35f;
            }
        }

        private static Pill MessagePill(Transform arena, string key)
        {
            Pill msg = MakePill(arena, "Message", Palette.White, 90f, 46f, 48f, key, FontRole.Title, 50f,
                Palette.Navy);
            Place(msg.Rect, BottomCenter, BottomCenter, Vector2.zero, msg.Rect.sizeDelta);
            return msg;
        }

        private static void WireStage(StageScreenBase screen, StageParts parts)
        {
            Set(screen, "nextButton", parts.Next);
            Set(screen, "nextBackground", parts.NextPill.Background);
            Set(screen, "nextLabel", parts.NextPill.Label);
            Set(screen, "nextShadow", parts.NextPill.Rect.GetComponent<Shadow>());
            Set(screen, "counterText", parts.Counter);
            Set(screen, "gauge", parts.Gauge);
            if (parts.Message != null)
                Set(screen, "messageText", parts.Message);
        }

        private static ScreenBase BuildStage1(Transform parent, GameConfig config)
        {
            RectTransform root = ScreenRoot("Stage1", parent);
            var screen = root.gameObject.AddComponent<Stage1Screen>();
            StageParts parts = BuildStageFrame(root, "s1.title", 0, "", "common.next");
            parts.Counter.SetRaw(Mathf.CeilToInt(config.s1TimeLimitSeconds).ToString());

            Pill kills = MakePill(parts.Arena, "KillCount", Palette.WhiteSoft, 84f, 35f, 38f, "", FontRole.Title,
                58f, Palette.Navy);
            Place(kills.Rect, TopLeft, TopLeft, Vector2.zero, kills.Rect.sizeDelta);
            kills.Label.SetRaw("0 / " + config.s1TargetKills);

            RectTransform bush = BuildBush(parts.Arena, new Vector2(0f, -235f));
            Image monsterBody;
            RectTransform monster = BuildMonster(parts.Arena, new Vector2(0f, 60f), 460f, out monsterBody);
            RectTransform alert = BuildAlertMark(parts.Arena, new Vector2(0f, 90f));
            parts.Message = MessagePill(parts.Arena, "s1.appear").Label;

            WireStage(screen, parts);
            Set(screen, "killCountText", kills.Label);
            Set(screen, "monster", monster);
            Set(screen, "monsterBody", monsterBody);
            Set(screen, "monsterGroup", monster.GetComponent<CanvasGroup>());
            Set(screen, "eyesNormal", monster.Find("Eyes").gameObject);
            Set(screen, "eyesDefeated", monster.Find("EyesDefeated").gameObject);
            Set(screen, "bush", bush);
            Set(screen, "alertMark", alert);
            Set(screen, "completePanel", BuildCompletePanel(root, holder =>
            {
                Image unused;
                BuildMonster(holder, Vector2.zero, 200f, out unused);
            }));
            return screen;
        }

        /// <summary>몬스터가 숨어 있는 덤불. 기준점이 아래쪽이라 예고 때 밑동을 축으로 흔들린다.</summary>
        private static RectTransform BuildBush(Transform parent, Vector2 basePosition)
        {
            RectTransform root = Place(Node("Bush", parent), Center, BottomCenter, basePosition,
                new Vector2(560f, 220f));
            Color back = new Color32(0x5B, 0xC9, 0x8F, 0xFF);
            Color front = Palette.Green;

            Place(Img(root, "BackL", back, Circle), BottomCenter, Center, new Vector2(-170f, 80f), new Vector2(210f, 170f));
            Place(Img(root, "BackR", back, Circle), BottomCenter, Center, new Vector2(170f, 80f), new Vector2(210f, 170f));
            Place(Img(root, "Middle", front, Circle), BottomCenter, Center, new Vector2(0f, 105f), new Vector2(280f, 220f));
            Place(Img(root, "FrontL", front, Circle), BottomCenter, Center, new Vector2(-95f, 55f), new Vector2(200f, 120f));
            Place(Img(root, "FrontR", front, Circle), BottomCenter, Center, new Vector2(95f, 55f), new Vector2(200f, 120f));
            return root;
        }

        /// <summary>등장 예고 "!" (노란 원).</summary>
        private static RectTransform BuildAlertMark(Transform parent, Vector2 position)
        {
            Image circle = Img(parent, "AlertMark", Palette.Yellow, Circle);
            Place(circle, Center, Center, position, new Vector2(130f, 130f));
            var shadow = circle.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Palette.YellowShadow;
            shadow.effectDistance = new Vector2(0f, -8f);
            LocText mark = Txt(circle.transform, "Mark", "", FontRole.Title, 96f, Palette.Navy);
            Stretch(mark.Label.rectTransform);
            mark.SetRaw("!");
            circle.gameObject.SetActive(false);
            return circle.rectTransform;
        }

        /// <summary>몬스터 자리표시 (시안 SVG 비율 200×180, 24cqw). 피격 시 몸 색이 번쩍인다.</summary>
        private static RectTransform BuildMonster(Transform parent, Vector2 position, float width, out Image body)
        {
            float s = width / 200f;
            RectTransform root = Place(Node("Monster", parent), Center, Center, position,
                new Vector2(width, 180f * s));
            System.Func<float, float, Vector2> P = (cx, cy) => new Vector2((cx - 100f) * s, (90f - cy) * s);

            // 뿔 (몸 뒤)
            for (int i = 0; i < 2; i++)
            {
                Image horn = Img(root, i == 0 ? "HornL" : "HornR", Palette.PurpleDeep);
                Place(horn, Center, Center, P(i == 0 ? 70f : 130f, 32f), new Vector2(24f * s, 24f * s));
                horn.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            body = Img(root, "Body", Palette.Purple, Circle);
            Place(body, Center, Center, P(100f, 100f), new Vector2(160f * s, 136f * s));

            // 화난 눈썹 + 눈(평소 / 쓰러짐 ×) + 입
            RectTransform eyes = Place(Node("Eyes", root), Center, Center, Vector2.zero, Vector2.zero);
            RectTransform deadEyes = Place(Node("EyesDefeated", root), Center, Center, Vector2.zero, Vector2.zero);

            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                Vector2 eyePos = P(left ? 80f : 120f, 110f);

                Image brow = RoundImg(root, left ? "BrowL" : "BrowR", Palette.Navy, 4f * s);
                Place(brow, Center, Center, P(left ? 76f : 124f, 93f), new Vector2(28f * s, 7f * s));
                brow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, left ? -20f : 20f);

                Place(Img(eyes, left ? "EyeL" : "EyeR", Palette.Navy, Circle), Center, Center, eyePos,
                    new Vector2(18f * s, 18f * s));

                for (int j = 0; j < 2; j++)
                {
                    Image stroke = RoundImg(deadEyes, (left ? "XL" : "XR") + j, Palette.Navy, 3f * s);
                    Place(stroke, Center, Center, eyePos, new Vector2(22f * s, 6f * s));
                    stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, j == 0 ? 45f : -45f);
                }
            }

            deadEyes.gameObject.SetActive(false);

            Image mouth = RadialImg(root, "Mouth", Palette.Navy, Ring, 3, 0.5f);
            Place(mouth, Center, Center, P(100f, 142f), new Vector2(32f * s, 20f * s));

            root.gameObject.AddComponent<CanvasGroup>();
            return root;
        }

        private static ScreenBase BuildStage2(Transform parent)
        {
            RectTransform root = ScreenRoot("Stage2", parent);
            var screen = root.gameObject.AddComponent<Stage2Screen>();
            StageParts parts = BuildStageFrame(root, "s2.title", 1, "s2.count", "common.next");

            // 유지 진행 링 (9cqw): 흰 원 + 초록 고리가 위에서부터 시계 방향으로 참 + 남은 초
            Image ringBg = Img(parts.Arena, "HoldRing", Palette.White, Circle);
            Place(ringBg, TopRight, TopRight, new Vector2(-38f, -38f), new Vector2(173f, 173f));
            Image ringTrack = Img(ringBg.transform, "Track", new Color32(0xE4, 0xE9, 0xF7, 0xFF), Ring);
            Stretch(ringTrack.rectTransform, 10f, 10f, 10f, 10f);
            Image ringFill = RadialImg(ringBg.transform, "Fill", Palette.Green, Ring, 2, 0f);
            Stretch(ringFill.rectTransform, 10f, 10f, 10f, 10f);
            LocText ringText = Txt(ringBg.transform, "Seconds", "", FontRole.Title, 60f, Palette.Navy);
            Stretch(ringText.Label.rectTransform);
            ringText.SetRaw("3");

            // 로켓이 서는 바닥 높이 (플레이 영역 아래에서부터 px). 받침대 없음.
            const float groundY = 230f;

            // 연기 (뒤) → 로켓
            SmokePuffs smoke = BuildSmoke(parts.Arena, 28);

            RectTransform flame;
            CanvasGroup flameGroup;
            RectTransform rocket = BuildRocket(parts.Arena, Vector2.zero, 400f, out flame, out flameGroup);
            rocket.anchorMin = rocket.anchorMax = BottomCenter;
            rocket.pivot = BottomCenter;
            rocket.anchoredPosition = new Vector2(0f, groundY - RocketFeetFromBottom); // 날개 끝이 바닥 높이에 오게
            flameGroup.alpha = 0.3f;

            parts.Message = MessagePill(parts.Arena, "s2.guide").Label;

            WireStage(screen, parts);
            Set(screen, "ringFill", ringFill);
            Set(screen, "ringText", ringText);
            Set(screen, "rocket", rocket);
            Set(screen, "flame", flame);
            Set(screen, "flameGroup", flameGroup);
            Set(screen, "smoke", smoke);
            Set(screen, "completePanel", BuildCompletePanel(root, holder =>
            {
                RectTransform iconFlame;
                CanvasGroup iconFlameGroup;
                BuildRocket(holder, Vector2.zero, 210f, out iconFlame, out iconFlameGroup);
                iconFlameGroup.alpha = 1f;
            }));
            return screen;
        }

        /// <summary>400px 로켓에서 날개 끝(시안 y≈124)이 로켓 사각형 아래끝(y=160)보다 위에 있는 거리.</summary>
        private const float RocketFeetFromBottom = 90f;

        /// <summary>연기 구름 풀 (흰 원 count 개, 처음엔 모두 꺼짐).</summary>
        private static SmokePuffs BuildSmoke(Transform parent, int count)
        {
            RectTransform root = Place(Node("Smoke", parent), Center, Center, Vector2.zero, Vector2.zero);
            Color puffColor = new Color32(0xF4, 0xF6, 0xFB, 0xFF);
            var puffs = new Image[count];
            for (int i = 0; i < count; i++)
            {
                puffs[i] = Img(root, "Puff" + i, puffColor, Circle);
                Place(puffs[i], Center, Center, Vector2.zero, new Vector2(80f, 80f));
                puffs[i].gameObject.SetActive(false);
            }

            var smoke = root.gameObject.AddComponent<SmokePuffs>();
            SetArray(smoke, "puffs", puffs);
            return smoke;
        }

        /// <summary>
        /// 단계 완료 카드 (화면 맨 앞): 어두운 배경 + 가운데 흰 카드(제목 · 아이콘 × 개수 · 큰 "다음 재활" 버튼).
        /// </summary>
        private static StageCompletePanel BuildCompletePanel(RectTransform screenRoot, System.Action<Transform> buildIcon)
        {
            const float cardWidth = 820f;
            const float cardHeight = 600f;

            RectTransform root = Stretch(Node("CompletePanel", screenRoot));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Color dimColor = Palette.Navy;
            dimColor.a = 0.45f;
            Image dim = Img(root, "Dim", dimColor);
            dim.raycastTarget = true; // 뒤쪽 화면 터치 막기
            Stretch(dim.rectTransform);

            Image card = RoundImg(root, "Card", Palette.White, 56f);
            card.raycastTarget = true;
            Place(card, Center, Center, new Vector2(0f, 20f), new Vector2(cardWidth, cardHeight));
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Palette.DisabledShadow;
            shadow.effectDistance = new Vector2(0f, -16f);
            Transform c = card.transform;

            LocText title = Txt(c, "Title", "common.goodJob", FontRole.Title, 66f, Palette.Navy);
            Place(title, TopCenter, TopCenter, new Vector2(0f, -56f), new Vector2(cardWidth - 60f, 90f));

            RectTransform icon = Place(Node("Icon", c), Center, Center, new Vector2(-130f, 30f), new Vector2(220f, 220f));
            buildIcon(icon);

            LocText count = Txt(c, "Count", "", FontRole.Title, 120f, Palette.Navy, TextAlignmentOptions.Left);
            Place(count, Center, MiddleLeft, new Vector2(10f, 30f), new Vector2(340f, 150f));
            count.SetRaw("× 0");

            Pill nextPill;
            Button next = Btn(c, "NextButton", Palette.Yellow, Palette.YellowShadow, 130f, 110f, "common.next",
                FontRole.Title, 58f, Palette.ButtonText, out nextPill);
            Place(nextPill.Rect, BottomCenter, BottomCenter, new Vector2(0f, 56f), nextPill.Rect.sizeDelta);

            var panel = root.gameObject.AddComponent<StageCompletePanel>();
            Set(panel, "group", group);
            Set(panel, "card", card.rectTransform);
            Set(panel, "title", title);
            Set(panel, "count", count);
            Set(panel, "nextButton", next);
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// 로켓 자리표시 (시안 SVG 비율 80×160). 불꽃 묶음(바깥 분홍 · 노랑 · 안쪽 흰 심지)은
        /// 노즐 위치가 기준점이라 세로 배율을 키우면 아래로 길어진다. 밝기는 CanvasGroup.
        /// </summary>
        private static RectTransform BuildRocket(Transform parent, Vector2 position, float height,
            out RectTransform flame, out CanvasGroup flameGroup)
        {
            float s = height / 160f;
            RectTransform root = Place(Node("Rocket", parent), Center, Center, position,
                new Vector2(80f * s, 160f * s));
            System.Func<float, float, Vector2> P = (cx, cy) => new Vector2((cx - 40f) * s, (80f - cy) * s);

            flame = Place(Node("Flame", root), Center, Center, P(40f, 118f), Vector2.zero);
            flameGroup = flame.gameObject.AddComponent<CanvasGroup>();
            Color glow = Palette.PinkDeep;
            glow.a = 0.75f;
            Place(Img(flame, "Glow", glow, Circle), Center, Center, new Vector2(0f, -26f * s),
                new Vector2(32f * s, 58f * s));
            Place(Img(flame, "Fire", Palette.Yellow, Circle), Center, Center, new Vector2(0f, -20f * s),
                new Vector2(24f * s, 44f * s));
            Place(Img(flame, "Core", Palette.White, Circle), Center, Center, new Vector2(0f, -12f * s),
                new Vector2(11f * s, 22f * s));

            // 노즐
            Place(RoundImg(root, "Nozzle", Palette.Navy, 3f * s), Center, Center, P(40f, 119f),
                new Vector2(22f * s, 8f * s));

            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                Image fin = Img(root, left ? "FinL" : "FinR", Palette.PinkDeep);
                Place(fin, Center, Center, P(left ? 18f : 62f, 110f), new Vector2(20f * s, 20f * s));
                fin.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 몸통 (윗부분은 덜 둥글게 — 코 밑으로 깔끔하게 이어지도록)
            Image outline = RoundImg(root, "Outline", Palette.Navy, 10f * s);
            Place(outline, Center, Center, P(40f, 75f), new Vector2(44f * s, 90f * s));
            Image body = RoundImg(outline.transform, "Body", Palette.White, 7f * s);
            Stretch(body.rectTransform, 4f * s, 4f * s, 4f * s, 4f * s);

            // 뾰족한 분홍 코 (몸통 앞): 남색 삼각형 테두리 + 분홍 삼각형 + 이음매 띠
            Place(Img(root, "Nose", Palette.Navy, Triangle), Center, Center, P(40f, 19f), new Vector2(48f * s, 34f * s));
            Place(Img(root, "NoseTip", Palette.PinkDeep, Triangle), Center, Center, P(40f, 19.25f),
                new Vector2(36f * s, 25.5f * s));
            Place(RoundImg(root, "Collar", Palette.Navy, 3f * s), Center, Center, P(40f, 35f), new Vector2(46f * s, 6f * s));

            Image window = Img(root, "Window", Palette.Navy, Circle);
            Place(window, Center, Center, P(40f, 66f), new Vector2(26f * s, 26f * s));
            Stretch(Img(window.transform, "Glass", Palette.Purple, Circle).rectTransform, 4f * s, 4f * s, 4f * s, 4f * s);
            return root;
        }

        private static ScreenBase BuildStage3(Transform parent)
        {
            RectTransform root = ScreenRoot("Stage3", parent);
            var screen = root.gameObject.AddComponent<Stage3Screen>();
            StageParts parts = BuildStageFrame(root, "s3.title", 2, "s3.count", "common.allDone");

            Transform arena = parts.Arena;

            // 카운트다운 링 (노트 뒤): 노트 등장부터 위에서 시계 방향으로 한 바퀴 차오르고, 꽉 찬 순간이 "지금".
            Color ringTrackColor = Palette.White;
            ringTrackColor.a = 0.7f;
            Place(Img(arena, "HoldRingTrack", ringTrackColor, Ring), Center, Center, new Vector2(0f, NoteY),
                new Vector2(380f, 380f));
            Image countRing = RadialImg(arena, "CountRing", Palette.Green, Ring, 2, 0f);
            Place(countRing, Center, Center, new Vector2(0f, NoteY), new Vector2(380f, 380f));

            // 노트: 등급마다 모양과 색을 모두 다르게 (살짝 보라 원 / 보통 초록 둥근 사각 / 세게 분홍 마름모) + 흰 테두리
            RectTransform note = Place(Node("Note", arena), Center, Center, new Vector2(0f, NoteY), new Vector2(300f, 300f));
            var noteGroup = note.gameObject.AddComponent<CanvasGroup>();
            var shapes = new GameObject[3];
            // 바탕은 연한 색, 그 위 버티기 게이지(진한 색)가 아래부터 찬다 (RectMask2D 높이로 조절).
            shapes[0] = NoteShape(note, "Soft", Pale(Palette.Purple), Circle, 270f, 0f, 0f);
            shapes[1] = NoteShape(note, "Normal", Pale(Palette.Green), Round, 240f, 0f, 56f);
            shapes[2] = NoteShape(note, "Strong", Pale(Palette.PinkDeep), null, 200f, 45f, 0f);

            float[] halfHeights = { 135f, 120f, 200f * 0.7071f };
            RectTransform meter = Node("Meter", note);
            meter.anchorMin = meter.anchorMax = Center;
            meter.pivot = BottomCenter;
            meter.sizeDelta = new Vector2(300f, 0f);
            meter.gameObject.AddComponent<RectMask2D>();
            var meterShapes = new GameObject[3];
            meterShapes[0] = NoteShape(meter, "SoftFill", Palette.Purple, Circle, 270f, 0f, 0f, false);
            meterShapes[1] = NoteShape(meter, "NormalFill", Palette.Green, Round, 240f, 0f, 56f, false);
            meterShapes[2] = NoteShape(meter, "StrongFill", Palette.PinkDeep, null, 200f, 45f, 0f, false);
            for (int i = 0; i < meterShapes.Length; i++)
            {
                // 마스크 높이가 바뀌어도 노트 가운데에 그대로 있도록 마스크 아래끝 기준으로 고정
                var rt = (RectTransform)meterShapes[i].transform;
                rt.anchorMin = rt.anchorMax = BottomCenter;
                rt.anchoredPosition = new Vector2(0f, halfHeights[i]);
            }

            LocText label = Txt(note, "Label", "s3.soft", FontRole.Title, 62f, Palette.White);
            Stretch(label.Label.rectTransform);
            note.gameObject.SetActive(false);

            // 카운트 배지 (노트 위): 3 · 2 · 1 · 지금!
            Image badge = RoundImg(arena, "CountBadge", Palette.Yellow, 60f);
            Place(badge, Center, Center, new Vector2(0f, NoteY + 285f), new Vector2(240f, 120f));
            var badgeShadow = badge.gameObject.AddComponent<Shadow>();
            badgeShadow.effectColor = Palette.YellowShadow;
            badgeShadow.effectDistance = new Vector2(0f, -10f);
            LocText count = Txt(badge.transform, "Count", "", FontRole.Title, 84f, Palette.Navy);
            Stretch(count.Label.rectTransform);
            count.SetRaw("3");
            badge.gameObject.SetActive(false);

            // 박자 점 4개 (노트 아래)
            var dots = new Image[4];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = Img(arena, "Beat" + (i + 1), Palette.Navy, Circle);
                Place(dots[i], Center, Center, new Vector2((i - 1.5f) * 64f, NoteY - 245f), new Vector2(34f, 34f));
            }

            BuildLegend(arena);
            parts.Message = MessagePill(arena, "s3.guide").Label;

            // 음악·효과음
            var music = root.gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            var sfx = root.gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            WireStage(screen, parts);
            Set(screen, "note", note);
            Set(screen, "noteGroup", noteGroup);
            SetArray(screen, "noteShapes", shapes);
            Set(screen, "noteLabel", label);
            Set(screen, "countRing", countRing);
            Set(screen, "meterMask", meter);
            SetArray(screen, "meterShapes", meterShapes);
            var screenSo = new SerializedObject(screen);
            SerializedProperty halves = screenSo.FindProperty("meterHalfHeights");
            halves.arraySize = halfHeights.Length;
            for (int i = 0; i < halfHeights.Length; i++)
                halves.GetArrayElementAtIndex(i).floatValue = halfHeights[i];
            screenSo.ApplyModifiedPropertiesWithoutUndo();
            Set(screen, "countBadge", badge.rectTransform);
            Set(screen, "countText", count);
            SetArray(screen, "beatDots", dots);
            Set(screen, "music", music);
            Set(screen, "sfx", sfx);
            Set(screen, "completePanel", BuildCompletePanel(root, holder =>
            {
                NoteShape(holder, "Soft", Palette.Purple, Circle, 70f, 0f, 0f).GetComponent<RectTransform>()
                    .anchoredPosition = new Vector2(-75f, 0f);
                NoteShape(holder, "Normal", Palette.Green, Round, 64f, 0f, 16f);
                NoteShape(holder, "Strong", Palette.PinkDeep, null, 54f, 45f, 0f).GetComponent<RectTransform>()
                    .anchoredPosition = new Vector2(75f, 0f);
            }));
            return screen;
        }

        /// <summary>3단계 노트가 놓이는 높이 (플레이 영역 가운데 기준).</summary>
        private const float NoteY = 30f;

        /// <summary>
        /// 노트 모양 하나: 흰 테두리(조금 큰 같은 모양) + 색 모양. sprite == null 이면 사각형(회전해서 마름모).
        /// cornerRadius 가 있으면 둥근 사각.
        /// </summary>
        private static GameObject NoteShape(Transform parent, string name, Color color, Sprite sprite, float size,
            float rotation, float cornerRadius, bool withOutline = true)
        {
            const float border = 12f;
            RectTransform root = Place(Node(name, parent), Center, Center, Vector2.zero, new Vector2(size, size));
            root.localRotation = Quaternion.Euler(0f, 0f, rotation);

            if (withOutline)
            {
                Image outline = Img(root, "Outline", Palette.White, sprite);
                if (cornerRadius > 0f)
                    SetRadius(outline, cornerRadius + border * 0.5f);
                Stretch(outline.rectTransform, -border, -border, -border, -border);
            }

            Image fill = Img(root, "Fill", color, sprite);
            if (cornerRadius > 0f)
                SetRadius(fill, cornerRadius);
            Stretch(fill.rectTransform);
            return root.gameObject;
        }

        /// <summary>버티기 게이지 바탕용 연한 색.</summary>
        private static Color Pale(Color color)
        {
            return Color.Lerp(color, Color.white, 0.6f);
        }

        /// <summary>노트 범례: 모양과 색을 모두 다르게 (살짝 원 / 보통 둥근 사각 / 세게 마름모).</summary>
        private static void BuildLegend(Transform arena)
        {
            Image bg = RoundImg(arena, "Legend", Palette.WhiteSoft, 38f);
            Place(bg, BottomLeft, BottomLeft, Vector2.zero, new Vector2(0f, 76f));
            var layout = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(31, 31, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            LegendItem(bg.transform, "Soft", Palette.Purple, Circle, 0f, 1f, "s3.soft");
            LegendSpacer(bg.transform);
            LegendItem(bg.transform, "Normal", Palette.Green, Round, 0f, SpriteBuilder.RoundRadius / 8f, "s3.normal");
            LegendSpacer(bg.transform);
            LegendItem(bg.transform, "Strong", Palette.PinkDeep, null, 45f, 1f, "s3.strong");
        }

        private static void LegendItem(Transform parent, string name, Color color, Sprite sprite, float rotation,
            float ppuMultiplier, string key)
        {
            RectTransform holder = Node(name + "Icon", parent);
            FixedLayoutSize(holder, 36f, 36f);
            Image icon = Img(holder, "Shape", color, sprite);
            if (sprite == Round)
            {
                icon.type = Image.Type.Sliced;
                icon.pixelsPerUnitMultiplier = ppuMultiplier;
            }

            float size = rotation != 0f ? 26f : 32f;
            Place(icon, Center, Center, Vector2.zero, new Vector2(size, size));
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);

            Txt(parent, name + "Label", key, FontRole.Body, 32f, Palette.Navy);
        }

        private static void LegendSpacer(Transform parent)
        {
            RectTransform spacer = Node("Spacer", parent);
            FixedLayoutSize(spacer, 19f, 10f);
        }

        // ---------- 화면: 결과 ----------

        private static ScreenBase BuildResult(Transform parent)
        {
            RectTransform root = ScreenRoot("Result", parent);
            var screen = root.gameObject.AddComponent<ResultScreen>();

            // 왼쪽 34cqw 열에 캐릭터
            BuildCharacter(root, "Character", new Vector2(-960f + 77f + 326f, -20f), 600f);

            const float rightX = 768f;
            LocText title = Txt(root, "Title", "done.title", FontRole.Title, 77f, Palette.Navy,
                TextAlignmentOptions.Left);
            Place(title, MiddleLeft, MiddleLeft, new Vector2(rightX, 200f), new Vector2(1100f, 100f));

            Pill homePill;
            Button home = Btn(root, "HomeButton", Palette.Yellow, Palette.YellowShadow, 110f, 77f, "done.home",
                FontRole.Title, 46f, Palette.ButtonText, out homePill);
            Place(homePill.Rect, MiddleLeft, MiddleLeft, new Vector2(rightX, -220f), homePill.Rect.sizeDelta);

            Set(screen, "homeButton", home);
            return screen;
        }

        // ---------- 보호자 모드 ----------

        private static GuardianPanel BuildGuardian(Transform canvas, ScreenRouter router)
        {
            Image dim = Img(canvas, "Guardian", Palette.Dim);
            dim.raycastTarget = true; // 아래 화면 터치 막기
            Stretch(dim.rectTransform);
            var panel = dim.gameObject.AddComponent<GuardianPanel>();

            Image card = RoundImg(dim.transform, "Card", Palette.White, 40f);
            card.raycastTarget = true;
            Place(card, Center, Center, Vector2.zero, new Vector2(1100f, 940f));
            Transform c = card.transform;

            Place(Txt(c, "Title", "guard.title", FontRole.Title, 58f, Palette.Navy, TextAlignmentOptions.Left),
                TopLeft, TopLeft, new Vector2(60f, -50f), new Vector2(700f, 80f));

            Pill closePill = MakePill(c, "CloseButton", Palette.Sky, 96f, 40f, 48f, "guard.close", FontRole.Title,
                38f, Palette.Navy);
            Button close = MakeButton(closePill, false, Color.clear);
            Place(closePill.Rect, TopRight, TopRight, new Vector2(-40f, -40f), closePill.Rect.sizeDelta);

            // 언어
            Place(Txt(c, "LanguageLabel", "guard.language", FontRole.Body, 40f, Palette.Navy,
                TextAlignmentOptions.Left), TopLeft, TopLeft, new Vector2(60f, -205f), new Vector2(280f, 60f));

            Pill koPill;
            Button ko = Btn(c, "KoreanButton", Palette.Sky, Palette.DisabledShadow, 96f, 48f, "guard.langKo",
                FontRole.Title, 40f, Palette.Navy, out koPill);
            Place(koPill.Rect, TopLeft, TopLeft, new Vector2(360f, -185f), koPill.Rect.sizeDelta);

            Pill enPill;
            Button en = Btn(c, "EnglishButton", Palette.Sky, Palette.DisabledShadow, 96f, 48f, "guard.langEn",
                FontRole.Title, 40f, Palette.Navy, out enPill);
            Place(enPill.Rect, TopLeft, TopLeft, new Vector2(620f, -185f), enPill.Rect.sizeDelta);

            // 입력 방식 (기기 선택 화면)
            Pill inputPill;
            Button input = Btn(c, "InputButton", Palette.Sky, Palette.DisabledShadow, 96f, 48f, "guard.input",
                FontRole.Title, 40f, Palette.Navy, out inputPill);
            Place(inputPill.Rect, TopLeft, TopLeft, new Vector2(60f, -330f), inputPill.Rect.sizeDelta);

            // 보정 다시 하기
            Pill recalPill;
            Button recal = Btn(c, "RecalibrateButton", Palette.Sky, Palette.DisabledShadow, 96f, 48f,
                "guard.recalibrate", FontRole.Title, 40f, Palette.Navy, out recalPill);
            Place(recalPill.Rect, TopLeft, TopLeft, new Vector2(60f, -470f), recalPill.Rect.sizeDelta);

            // 화면 세기 버튼 켜기/끄기
            Place(Txt(c, "MockPadLabel", "guard.mockPad", FontRole.Body, 40f, Palette.Navy,
                TextAlignmentOptions.Left), TopLeft, TopLeft, new Vector2(60f, -635f), new Vector2(520f, 60f));
            Pill padPill;
            Button pad = Btn(c, "MockPadButton", Palette.Sky, Palette.DisabledShadow, 96f, 48f, "guard.on",
                FontRole.Title, 40f, Palette.Navy, out padPill);
            Place(padPill.Rect, TopLeft, TopLeft, new Vector2(620f, -615f), padPill.Rect.sizeDelta);

            // 소리 켜기/끄기
            Place(Txt(c, "SoundLabel", "guard.sound", FontRole.Body, 40f, Palette.Navy,
                TextAlignmentOptions.Left), TopLeft, TopLeft, new Vector2(60f, -775f), new Vector2(520f, 60f));
            Pill soundPill;
            Button sound = Btn(c, "SoundButton", Palette.Sky, Palette.DisabledShadow, 96f, 48f, "guard.on",
                FontRole.Title, 40f, Palette.Navy, out soundPill);
            Place(soundPill.Rect, TopLeft, TopLeft, new Vector2(620f, -755f), soundPill.Rect.sizeDelta);

            Set(panel, "router", router);
            Set(panel, "koreanButton", ko);
            Set(panel, "englishButton", en);
            Set(panel, "koreanBackground", koPill.Background);
            Set(panel, "englishBackground", enPill.Background);
            Set(panel, "inputButton", input);
            Set(panel, "recalibrateButton", recal);
            Set(panel, "mockPadButton", pad);
            Set(panel, "mockPadState", padPill.Label);
            Set(panel, "soundButton", sound);
            Set(panel, "soundState", soundPill.Label);
            Set(panel, "closeButton", close);
            return panel;
        }

        // ---------- 화면: 기기 선택 ----------

        private static ScreenBase BuildDeviceSelect(Transform parent)
        {
            RectTransform root = ScreenRoot("DeviceSelect", parent);
            var screen = root.gameObject.AddComponent<DeviceSelectScreen>();

            Place(Txt(root, "Title", "dev.title", FontRole.Title, 70f, Palette.Navy), TopCenter, TopCenter,
                new Vector2(0f, -110f), new Vector2(1600f, 100f));

            Button ble = DeviceCard(root, "BleCard", new Vector2(-340f, 10f), Palette.Purple, "dev.bleTitle",
                "dev.bleDesc", BuildBleIcon);
            Button mock = DeviceCard(root, "MockCard", new Vector2(340f, 10f), Palette.Yellow, "dev.mockTitle",
                "dev.mockDesc", BuildMockIcon);
            var bleGroup = ble.gameObject.AddComponent<CanvasGroup>();

            Pill udpPill = MakePill(root, "UdpButton", Palette.WhiteSoft, 96f, 48f, 48f, "dev.udp", FontRole.Body,
                34f, Palette.Navy);
            Button udp = MakeButton(udpPill, false, Color.clear);
            Place(udpPill.Rect, BottomCenter, BottomCenter, new Vector2(0f, 150f), udpPill.Rect.sizeDelta);

            Set(screen, "mockButton", mock);
            Set(screen, "udpButton", udp);
            Set(screen, "bleButton", ble);
            Set(screen, "bleGroup", bleGroup);
            Set(screen, "udpRoot", udpPill.Rect.gameObject);
            return screen;
        }

        private static Button DeviceCard(Transform parent, string name, Vector2 position, Color iconColor,
            string titleKey, string descKey, System.Action<Transform> buildIcon)
        {
            const float width = 600f;
            Image card = RoundImg(parent, name, Palette.White, 48f);
            card.raycastTarget = true;
            Place(card, Center, Center, position, new Vector2(width, 440f));
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Palette.DisabledShadow;
            shadow.effectDistance = new Vector2(0f, -13f);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;

            Image icon = Img(card.transform, "Icon", iconColor, Circle);
            Place(icon, TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(160f, 160f));
            buildIcon(icon.transform);

            Place(Txt(card.transform, "Title", titleKey, FontRole.Title, 50f, Palette.Navy), TopCenter, TopCenter,
                new Vector2(0f, -220f), new Vector2(width - 40f, 70f));

            LocText desc = Txt(card.transform, "Desc", descKey, FontRole.Body, 32f, Palette.SubText);
            desc.Label.textWrappingMode = TextWrappingModes.Normal;
            desc.Label.alignment = TextAlignmentOptions.Top;
            Place(desc, TopCenter, TopCenter, new Vector2(0f, -300f), new Vector2(width - 80f, 100f));

            return button;
        }

        /// <summary>블루투스 아이콘 자리: 오른쪽으로 퍼지는 전파 3줄.</summary>
        private static void BuildBleIcon(Transform icon)
        {
            Place(Img(icon, "Dot", Palette.White, Circle), Center, Center, new Vector2(-34f, 0f),
                new Vector2(22f, 22f));
            float[] sizes = { 60f, 100f, 140f };
            for (int i = 0; i < sizes.Length; i++)
            {
                Image wave = RadialImg(icon, "Wave" + (i + 1), Palette.White, Ring, 1, 0.25f);
                Place(wave, Center, Center, new Vector2(-34f, 0f), new Vector2(sizes[i], sizes[i]));
                wave.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
        }

        /// <summary>모의 입력 아이콘 자리: 살짝·보통·세게 막대 3개.</summary>
        private static void BuildMockIcon(Transform icon)
        {
            float[] heights = { 34f, 58f, 82f };
            for (int i = 0; i < heights.Length; i++)
            {
                Image bar = RoundImg(icon, "Bar" + (i + 1), Palette.Navy, 8f);
                Place(bar, Center, new Vector2(0.5f, 0f), new Vector2((i - 1) * 32f, -40f),
                    new Vector2(20f, heights[i]));
            }
        }

        // ---------- 겹쳐 뜨는 요소 ----------

        /// <summary>화면 세기 버튼 4개 (모의 입력 전용, 왼쪽 아래).</summary>
        private static void BuildMockPad(Transform canvas)
        {
            RectTransform root = Stretch(Node("MockPad", canvas));
            var view = root.gameObject.AddComponent<MockPadView>();

            RectTransform row = Node("Buttons", root);
            Place(row, BottomLeft, BottomLeft, new Vector2(30f, 30f), new Vector2(0f, 96f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fit = row.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            string[] keys = { "mock.soft", "mock.normal", "mock.strong", "mock.max" };
            var buttons = new HoldButton[keys.Length];
            Color normal = new Color(1f, 1f, 1f, 0.7f);

            for (int i = 0; i < keys.Length; i++)
            {
                Image bg = RoundImg(row, "Pad" + (i + 1), normal, 30f);
                bg.raycastTarget = true;
                bg.rectTransform.sizeDelta = new Vector2(104f, 96f);
                Stretch(Txt(bg.transform, "Label", keys[i], FontRole.Title, 32f, Palette.Navy).Label.rectTransform);

                var hold = bg.gameObject.AddComponent<HoldButton>();
                Set(hold, "background", bg);
                var so = new SerializedObject(hold);
                so.FindProperty("normalColor").colorValue = normal;
                so.FindProperty("heldColor").colorValue = Palette.Pink;
                so.ApplyModifiedPropertiesWithoutUndo();
                buttons[i] = hold;
            }

            Set(view, "buttonsRoot", row.gameObject);
            SetArray(view, "buttons", buttons);
        }

        /// <summary>"테스트 모드" 표시와 연결 끊김 배너.</summary>
        private static void BuildInputStatus(Transform canvas)
        {
            RectTransform root = Stretch(Node("InputStatus", canvas));
            var view = root.gameObject.AddComponent<InputStatusView>();

            Color badgeColor = Palette.Navy;
            badgeColor.a = 0.8f;
            Pill badge = MakePill(root, "TestBadge", badgeColor, 48f, 24f, 24f, "badge.test", FontRole.Title, 32f,
                Palette.White);
            Place(badge.Rect, TopCenter, TopCenter, new Vector2(0f, -4f), badge.Rect.sizeDelta);
            badge.Rect.gameObject.SetActive(false);

            Pill banner = MakePill(root, "DisconnectBanner", Palette.PinkDeep, 90f, 48f, 45f, "conn.lost",
                FontRole.Title, 44f, Palette.White);
            Place(banner.Rect, TopCenter, TopCenter, new Vector2(0f, -70f), banner.Rect.sizeDelta);
            banner.Rect.gameObject.SetActive(false);

            Set(view, "testBadge", badge.Rect.gameObject);
            Set(view, "disconnectBanner", banner.Rect.gameObject);
        }

        /// <summary>디버그 오버레이 (F1). 게이지를 가리지 않게 게이지 왼쪽에 둔다.</summary>
        private static DebugOverlay BuildDebugOverlay(Transform canvas)
        {
            RectTransform root = Stretch(Node("DebugOverlay", canvas));
            var overlay = root.gameObject.AddComponent<DebugOverlay>();

            Color bgColor = Palette.Navy;
            bgColor.a = 0.88f;
            Image panel = RoundImg(root, "Panel", bgColor, 24f);
            panel.raycastTarget = true; // 패널 클릭이 모의 입력(빈 곳 누르기)으로 새지 않게
            const float width = 620f;
            Place(panel, TopRight, TopRight, new Vector2(-(Margin + GaugeWidth + GaugeGap), -PlayTop),
                new Vector2(width, 470f));
            Transform p = panel.transform;

            LocText body = Txt(p, "Body", "", FontRole.Body, 28f, Palette.White, TextAlignmentOptions.TopLeft);
            Place(body, TopLeft, TopLeft, new Vector2(24f, -20f), new Vector2(width - 48f, 240f));
            body.Label.lineSpacing = 4f;

            Place(Txt(p, "SliderLabel", "dbg.slider", FontRole.Body, 26f, new Color(1f, 1f, 1f, 0.7f),
                TextAlignmentOptions.Left), TopLeft, TopLeft, new Vector2(24f, -270f), new Vector2(width - 48f, 36f));
            Slider slider = BuildSlider(p, new Vector2(24f, -316f), width - 48f);
            var drag = slider.gameObject.AddComponent<SliderDrag>();

            Pill restPill = MakePill(p, "RestButton", Palette.Sky, 64f, 24f, 32f, "dbg.setRest", FontRole.Body, 28f,
                Palette.Navy);
            Button rest = MakeButton(restPill, false, Color.clear);
            Place(restPill.Rect, BottomLeft, BottomLeft, new Vector2(24f, 24f), restPill.Rect.sizeDelta);

            Pill maxPill = MakePill(p, "MaxButton", Palette.Sky, 64f, 24f, 32f, "dbg.setMax", FontRole.Body, 28f,
                Palette.Navy);
            Button max = MakeButton(maxPill, false, Color.clear);
            Place(maxPill.Rect, BottomLeft, BottomLeft, new Vector2(260f, 24f), maxPill.Rect.sizeDelta);

            Set(overlay, "panel", panel.gameObject);
            Set(overlay, "body", body.Label);
            Set(overlay, "slider", slider);
            Set(overlay, "sliderDrag", drag);
            Set(overlay, "restButton", rest);
            Set(overlay, "maxButton", max);
            panel.gameObject.SetActive(false);
            return overlay;
        }

        private static Slider BuildSlider(Transform parent, Vector2 topLeft, float width)
        {
            const float height = 48f;
            RectTransform root = Node("Slider", parent);
            Place(root, TopLeft, TopLeft, topLeft, new Vector2(width, height));

            Image background = RoundImg(root, "Background", new Color(1f, 1f, 1f, 0.25f), 10f);
            RectTransform bgRt = background.rectTransform;
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 20f);
            background.raycastTarget = true;

            RectTransform fillArea = Node("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(12f, -10f);
            fillArea.offsetMax = new Vector2(-12f, 10f);
            Image fill = RoundImg(fillArea, "Fill", Palette.Pink, 10f);
            fill.rectTransform.sizeDelta = new Vector2(12f, 0f);

            RectTransform handleArea = Node("Handle Slide Area", root);
            Stretch(handleArea, 12f, 0f, 12f, 0f);
            Image handle = Img(handleArea, "Handle", Palette.White, Circle);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(height, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 0f;
            return slider;
        }
    }
}
