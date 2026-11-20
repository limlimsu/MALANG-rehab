using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Malang.Training
{
    [Serializable]
    public class SessionRecord
    {
        public string date;
        public float durationSeconds;
        public StageResult[] stages;
    }

    /// <summary>이름 없이 기기 내부에만 저장하는 훈련 기록.</summary>
    public static class SessionHistory
    {
        private static string DefaultPath => Path.Combine(Application.persistentDataPath, "sessions.jsonl");

        public static bool Save(SessionRecord record, string path = null)
        {
            if (record == null || record.stages == null || record.stages.Length == 0)
                return false;

            try
            {
                string file = path ?? DefaultPath;
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.AppendAllText(file, JsonUtility.ToJson(record) + Environment.NewLine);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SessionHistory: 기록 저장 실패: " + e.Message);
                return false;
            }
        }

        public static List<SessionRecord> Recent(int count, string path = null)
        {
            var records = new List<SessionRecord>();
            try
            {
                string file = path ?? DefaultPath;
                if (!File.Exists(file))
                    return records;

                // shortcut: 전체 파일을 읽는다. 기록이 많아지면 끝에서 최근 항목만 읽도록 바꾼다.
                string[] lines = File.ReadAllLines(file);
                for (int i = lines.Length - 1; i >= 0 && records.Count < count; i--)
                {
                    try
                    {
                        SessionRecord record = JsonUtility.FromJson<SessionRecord>(lines[i]);
                        if (record != null && record.stages != null && record.stages.Length > 0)
                            records.Add(record);
                    }
                    catch (ArgumentException) { } // 불완전한 마지막 줄은 건너뛴다.
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("SessionHistory: 기록 읽기 실패: " + e.Message);
            }

            return records;
        }
    }
}
