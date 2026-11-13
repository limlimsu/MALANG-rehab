using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Malang.EditorTools
{
    /// <summary>
    /// - 어떤 씬을 열어 두었든 Play 를 누르면 항상 Main.unity 부터 실행한다.
    ///   (메뉴 MALANG > Play From Main Scene 으로 켜고 끔, 기본 켜짐)
    /// - Main.unity 가 없거나 코드보다 오래된 구성(SceneVersion 불일치)이면 다시 빌드할지 묻는다:
    ///   1) 스크립트 컴파일 직후
    ///   2) Play 를 누르면 → 들어가자마자 Play 를 빠져나온 뒤
    ///   (Play 직전 취소는 Unity 6 에서 확실하지 않아 쓰지 않는다)
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMain
    {
        private const string PrefKey = "Malang.PlayFromMain";
        private const string PendingKey = "Malang.PendingRebuildPrompt";
        private const string OpenedMainKey = "Malang.OpenedMainOnStartup";
        private const string MenuPath = "MALANG/Play From Main Scene";
        private static readonly Regex VersionPattern = new Regex(@"sceneVersion:\s*(\d+)");

        static PlayFromMain()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += OnEditorLoaded;
        }

        private static bool Enabled
        {
            get { return EditorPrefs.GetBool(PrefKey, true); }
            set { EditorPrefs.SetBool(PrefKey, value); }
        }

        [MenuItem(MenuPath, priority = 40)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        [MenuItem("MALANG/Open Main Scene", priority = 1)]
        private static void OpenMain()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
        }

        private static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneBuilder.ScenePath)
                : null;
        }

        /// <summary>컴파일·에디터 시작 직후.</summary>
        private static void OnEditorLoaded()
        {
            Apply();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            OpenMainIfEmpty();

            if (SessionState.GetBool(PendingKey, false) || IsOutdated())
            {
                SessionState.SetBool(PendingKey, false);
                PromptRebuild();
            }
        }

        /// <summary>
        /// 새로 클론한 프로젝트는 "마지막으로 연 씬" 정보(Library, git 제외)가 없어 빈 Untitled 씬으로 열린다.
        /// 에디터를 켠 뒤 처음 한 번, 저장 안 된 빈 씬 하나만 열려 있으면 Main.unity 를 연다.
        /// </summary>
        private static void OpenMainIfEmpty()
        {
            if (SessionState.GetBool(OpenedMainKey, false))
                return;
            SessionState.SetBool(OpenedMainKey, true);

            if (EditorSceneManager.sceneCount != 1 || !File.Exists(SceneBuilder.ScenePath))
                return;

            UnityEngine.SceneManagement.Scene active = EditorSceneManager.GetActiveScene();
            bool untitled = string.IsNullOrEmpty(active.path);
            if (untitled && !active.isDirty)
                EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (IsOutdated())
                    {
                        SessionState.SetBool(PendingKey, true);
                        EditorApplication.ExitPlaymode();
                    }
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    if (SessionState.GetBool(PendingKey, false))
                    {
                        SessionState.SetBool(PendingKey, false);
                        EditorApplication.delayCall += PromptRebuild;
                    }
                    break;
            }
        }

        private static bool IsOutdated()
        {
            return ReadSceneVersion() != SceneBuilder.SceneVersion;
        }

        private static void PromptRebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !IsOutdated())
                return;

            int version = ReadSceneVersion();
            string reason = version < 0
                ? "Main.unity 가 없습니다."
                : "Main.unity 가 예전 구성(v" + version + ")입니다. 현재 코드는 v" + SceneBuilder.SceneVersion + " 입니다.";

            bool rebuild = EditorUtility.DisplayDialog(
                "MALANG: 씬 다시 만들기 필요",
                reason + "\n\n지금 MALANG > Build Scene 을 실행할까요? 끝나면 Play 를 누르세요.",
                "지금 빌드", "나중에");

            if (rebuild)
            {
                SceneBuilder.Build();
                Apply();
            }
        }

        /// <summary>씬 파일에서 AppManager.sceneVersion 값을 읽는다. 없으면 0, 파일이 없으면 -1.</summary>
        private static int ReadSceneVersion()
        {
            if (!File.Exists(SceneBuilder.ScenePath))
                return -1;

            Match match = VersionPattern.Match(File.ReadAllText(SceneBuilder.ScenePath));
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }
}
