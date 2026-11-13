using System;
using UnityEngine;

namespace Malang.Audio
{
    /// <summary>
    /// 음악·효과음을 코드로 합성한다 (외부 음원 파일 없음).
    /// - 3단계 배경 음악: 모차르트 「아이네 클라이네 나흐트무지크」 K.525 1악장 첫 4마디 (공공 저작물).
    ///   통통 튀는 멜로디 + 한 옥타브 아래 같은 멜로디(원곡처럼 모두 함께 연주) + 쿵(매 박)·칫(엇박) 드럼.
    ///   4/4 박자 4마디를 끊김 없이 반복 재생하도록 만든다 (꼬리 소리는 처음으로 감김).
    /// - 효과음: 카운트 "삑", "지금!" 강조음, 성공 "띠링", 실패 "뿌웅".
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        private const float MelodyAmp = 0.16f;
        private const float BassAmp = 0.18f;
        private const float KickAccentAmp = 0.5f;
        private const float KickAmp = 0.28f;
        private const float HatAmp = 0.045f;
        private const float Articulation = 0.85f;   // 음 길이 대비 실제로 울리는 비율 (살짝 끊어서)

        private struct Note
        {
            public float Beat;
            public float Length;
            public int Midi;

            public Note(float beat, float length, int midi)
            {
                Beat = beat;
                Length = length;
                Midi = midi;
            }
        }

        // MIDI 번호: D4 62, F#4 66, G4 67, A4 69, B4 71, C5 72, D5 74
        private static readonly Note[] Melody =
        {
            // 1마디: 솔 . 레 솔 . 레
            new Note(0f, 1f, 67), new Note(1.5f, 0.5f, 62), new Note(2f, 1f, 67), new Note(3.5f, 0.5f, 62),
            // 2마디: 솔 레 솔 시 레~
            new Note(4f, 0.5f, 67), new Note(4.5f, 0.5f, 62), new Note(5f, 0.5f, 67), new Note(5.5f, 0.5f, 71),
            new Note(6f, 2f, 74),
            // 3마디: 도 . 라 도 . 라
            new Note(8f, 1f, 72), new Note(9.5f, 0.5f, 69), new Note(10f, 1f, 72), new Note(11.5f, 0.5f, 69),
            // 4마디: 도 라 파# 라 레~
            new Note(12f, 0.5f, 72), new Note(12.5f, 0.5f, 69), new Note(13f, 0.5f, 66), new Note(13.5f, 0.5f, 69),
            new Note(14f, 2f, 62)
        };

        private const int LoopBeats = 16;

        public static AudioClip NachtmusikLoop(float bpm)
        {
            double beatSec = 60.0 / bpm;
            int length = (int)Math.Round(LoopBeats * beatSec * SampleRate);
            var buffer = new float[length];

            foreach (Note n in Melody)
            {
                double start = n.Beat * beatSec;
                double duration = n.Length * beatSec * Articulation;
                AddTone(buffer, start, duration, MidiToHz(n.Midi), MelodyAmp, Wave.SoftSquare);
                AddTone(buffer, start, duration, MidiToHz(n.Midi - 12), BassAmp, Wave.Triangle);
            }

            var random = new System.Random(525);
            for (int b = 0; b < LoopBeats; b++)
            {
                AddKick(buffer, b * beatSec, b % 4 == 0 ? KickAccentAmp : KickAmp);
                AddHat(buffer, (b + 0.5) * beatSec, HatAmp, random);
            }

            SoftClip(buffer);
            return MakeClip("Nachtmusik", buffer);
        }

        /// <summary>카운트다운 "삑".</summary>
        public static AudioClip Tick()
        {
            var buffer = new float[Samples(0.14)];
            AddTone(buffer, 0, 0.07, 1568f, 0.35f, Wave.Sine, 0.003, 0.04);
            return MakeClip("Tick", buffer);
        }

        /// <summary>"지금!" 강조음 (쿵 + 밝은 화음).</summary>
        public static AudioClip NowAccent()
        {
            var buffer = new float[Samples(0.3)];
            AddKick(buffer, 0, 0.7f);
            AddTone(buffer, 0, 0.16, 784f, 0.22f, Wave.SoftSquare, 0.003, 0.08);
            AddTone(buffer, 0, 0.16, 1175f, 0.16f, Wave.SoftSquare, 0.003, 0.08);
            SoftClip(buffer);
            return MakeClip("NowAccent", buffer);
        }

        /// <summary>성공 "띠링" (두 음이 올라감).</summary>
        public static AudioClip Good()
        {
            var buffer = new float[Samples(0.45)];
            AddTone(buffer, 0, 0.1, 1319f, 0.28f, Wave.Sine, 0.003, 0.06);
            AddTone(buffer, 0.09, 0.18, 1976f, 0.24f, Wave.Sine, 0.003, 0.12);
            return MakeClip("Good", buffer);
        }

        /// <summary>실패 "뿌웅" (부드럽게 내려감).</summary>
        public static AudioClip Miss()
        {
            int length = Samples(0.3);
            var buffer = new float[length];
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)SampleRate;
                double freq = 330 - 140 * (t / 0.3);
                double env = Math.Min(1.0, t / 0.01) * Math.Exp(-t * 7.0);
                buffer[i] += (float)(0.3 * env * TriangleWave(2 * Math.PI * freq * t));
            }

            return MakeClip("Miss", buffer);
        }

        // ---------- 합성 도우미 ----------

        private enum Wave
        {
            Sine,
            Triangle,
            SoftSquare
        }

        private static float MidiToHz(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        private static int Samples(double seconds)
        {
            return (int)Math.Ceiling(seconds * SampleRate);
        }

        /// <summary>
        /// 음 하나를 더한다. 버퍼 끝을 넘는 꼬리는 처음으로 감겨서 반복 재생 때 이음매가 자연스럽다.
        /// 엔벨로프: 짧은 어택 → 서서히 줄어듦 → 끝에서 release 동안 0 으로.
        /// </summary>
        private static void AddTone(float[] buffer, double start, double duration, float freq, float amp, Wave wave,
            double attack = 0.006, double release = 0.05)
        {
            int startIndex = (int)Math.Round(start * SampleRate);
            int total = Samples(duration + release);
            double phaseStep = 2 * Math.PI * freq / SampleRate;

            for (int i = 0; i < total; i++)
            {
                double t = i / (double)SampleRate;
                double env = Math.Min(1.0, t / attack) * (0.75 + 0.25 * Math.Exp(-t * 6.0));
                if (t > duration)
                    env *= Math.Max(0.0, 1.0 - (t - duration) / release);

                double phase = phaseStep * i;
                double s;
                switch (wave)
                {
                    case Wave.Triangle: s = TriangleWave(phase); break;
                    case Wave.SoftSquare: s = Math.Tanh(2.5 * Math.Sin(phase)) * 0.8; break;
                    default: s = Math.Sin(phase); break;
                }

                int index = (startIndex + i) % buffer.Length;
                buffer[index] += (float)(amp * env * s);
            }
        }

        private static double TriangleWave(double phase)
        {
            return 2.0 / Math.PI * Math.Asin(Math.Sin(phase));
        }

        /// <summary>쿵: 낮아지는 사인 + 빠른 감쇠.</summary>
        private static void AddKick(float[] buffer, double start, float amp)
        {
            int startIndex = (int)Math.Round(start * SampleRate);
            int total = Samples(0.18);
            double phase = 0;

            for (int i = 0; i < total; i++)
            {
                double t = i / (double)SampleRate;
                double freq = 48 + 80 * Math.Exp(-t * 30);
                phase += 2 * Math.PI * freq / SampleRate;
                double env = Math.Exp(-t * 18);
                int index = (startIndex + i) % buffer.Length;
                buffer[index] += (float)(amp * env * Math.Sin(phase));
            }
        }

        /// <summary>칫: 짧은 잡음.</summary>
        private static void AddHat(float[] buffer, double start, float amp, System.Random random)
        {
            int startIndex = (int)Math.Round(start * SampleRate);
            int total = Samples(0.04);

            for (int i = 0; i < total; i++)
            {
                double t = i / (double)SampleRate;
                double env = Math.Exp(-t * 90);
                int index = (startIndex + i) % buffer.Length;
                buffer[index] += (float)(amp * env * (random.NextDouble() * 2 - 1));
            }
        }

        private static void SoftClip(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (float)Math.Tanh(buffer[i] * 1.1);
        }

        private static AudioClip MakeClip(string name, float[] buffer)
        {
            AudioClip clip = AudioClip.Create(name, buffer.Length, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }
    }
}
