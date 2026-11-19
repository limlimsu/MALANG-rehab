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

        /// <summary>임포트·컴파일이 끝난 뒤 이만큼 에디터 업데이트가 조용히 지나가면 "시작 완료"로 본다.</summary>
        private const int StableUpdatesNeeded = 60;

        private static int stableUpdates;

        static PlayFromMain()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            stableUpdates = 0;
            EditorApplication.update += WaitUntilEditorSettles;
        }

        /// <summary>
        /// 새로 클론한 프로젝트는 첫 임포트가 길고, 그 사이 Unity 가 시작 씬을 다시 만들 수 있다.
        /// 컴파일·임포트가 모두 끝나고 에디터가 잠시 조용해진 뒤에 시작 처리를 한다.
        /// </summary>
        private static void WaitUntilEditorSettles()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                stableUpdates = 0;
                return;
            }

            if (++stableUpdates < StableUpdatesNeeded)
                return;

            EditorApplication.update -= WaitUntilEditorSettles;
            OnEditorLoaded();
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
        /// 에디터를 켠 뒤 처음 한 번, 저장 안 된 Untitled 씬 하나만 열려 있고
        /// 그 안이 비었거나 기본 오브젝트(카메라·조명)뿐이면 Main.unity 를 연다.
        /// </summary>
        private static void OpenMainIfEmpty()
        {
            if (SessionState.GetBool(OpenedMainKey, false))
                return;
            SessionState.SetBool(OpenedMainKey, true);

            if (!File.Exists(SceneBuilder.ScenePath))
            {
                UnityEngine.Debug.LogWarning("MALANG: " + SceneBuilder.ScenePath + " 이 없습니다. " +
                                             "브랜치를 확인하거나 MALANG > Build Scene 을 실행하세요.");
                return;
            }

            if (EditorSceneManager.sceneCount != 1)
                return;

            UnityEngine.SceneManagement.Scene active = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path))
                return; // 이미 저장된 씬이 열려 있음 (마지막으로 연 씬이 복원됨)

            if (active.isDirty && !HasOnlyDefaultObjects(active))
                return; // 사용자가 무언가 만든 새 씬은 건드리지 않음

            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            UnityEngine.Debug.Log("MALANG: 시작 씬이 비어 있어 " + SceneBuilder.ScenePath + " 을 열었습니다.");
        }

        /// <summary>새 씬 기본 구성(비었거나 Main Camera · Directional Light 만)인지.</summary>
        private static bool HasOnlyDefaultObjects(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (UnityEngine.GameObject go in scene.GetRootGameObjects())
            {
                if (go.name != "Main Camera" && go.name != "Directional Light" && go.name != "Global Volume")
                    return false;
            }

            return true;
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
