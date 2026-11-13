using System.Collections.Generic;
using UnityEngine;

namespace Malang.Training
{
    /// <summary>
    /// 오늘의 세션: 세션 시계와 단계 결과 모음.
    /// 시계는 단계 화면이 진행 중일 때만 흐른다 (센서 연결 끊김 동안 멈춤).
    /// M3: 결과를 모으고 Console 에 출력만 한다. M4 에서 세션 JSON 저장·경험치와 연결.
    /// </summary>
    public static class SessionManager
    {
        private static readonly List<StageResult> results = new List<StageResult>();

        public static bool IsActive { get; private set; }

        /// <summary>세션 시작 후 초 (일시정지 제외).</summary>
        public static double Time { get; private set; }

        public static IReadOnlyList<StageResult> Results => results;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            results.Clear();
            IsActive = false;
            Time = 0d;
        }

        public static void Begin()
        {
            results.Clear();
            Time = 0d;
            IsActive = true;
        }

        public static void Advance(float deltaTime)
        {
            Time += deltaTime;
        }

        public static void AddResult(StageResult result)
        {
            if (result == null)
                return;

            results.Add(result);
            Debug.Log("[Session] stage " + result.stage + " 결과: " + JsonUtility.ToJson(result));
        }

        public static void End()
        {
            IsActive = false;
        }
    }
}
