using System.Collections;
using Malang.Audio;
using Malang.Core;
using Malang.Training;
using UnityEngine;
using UnityEngine.UI;

namespace Malang.UI
{
    /// <summary>
    /// 3단계 · 리듬 타이밍 화면 (신호 → 반응). 화면에 보이는 것 = 판정 기준.
    /// - 카운트다운 마디: 노트가 나오고 링이 시계 방향으로 한 바퀴 차오른다 (3 · 2 · 1 + 삑).
    ///   링이 꽉 차는 순간이 "지금!" (링 번쩍 + 쿵).
    /// - 버티기 마디: 노트가 연해졌다가 아래부터 다시 차오르는 버티기 게이지가 된다.
    ///   등급 구간(게이지 초록 띠) 안에 있을 때만 차고, 꽉 차면 "좋아!".
    /// - 박자 점 4개가 현재 박을 표시한다(카운트다운 마디는 남색, 버티기 마디는 초록).
    /// - 규칙 시계는 오디오 재생 시계(dspTime)라 음악과 판정이 어긋나지 않는다.
    ///   센서 연결이 끊기면 음악도 함께 멈춘다. 소리를 꺼도(음소거) 시계는 그대로 흐른다.
    /// </summary>
    public class Stage3Screen : StageScreenBase
    {
        private const double ScheduleLeadSeconds = 0.1;
        private const float CountPop = 1.25f;
        private const float SuccessScale = 1.35f;
        private const float ShakePixels = 16f;
        private const float FailAlpha = 0.35f;
        private const float DotDimAlpha = 0.3f;
        private const float RingFlashSeconds = 0.25f;
        private const float RingFlashScale = 1.12f;

        private static AudioClip musicClip;
        private static float musicClipBpm;
        private static AudioClip tickClip;
        private static AudioClip nowClip;
        private static AudioClip goodClip;
        private static AudioClip missClip;

        [SerializeField] private RectTransform note;
        [SerializeField] private CanvasGroup noteGroup;
        [SerializeField] private GameObject[] noteShapes = new GameObject[3];
        [SerializeField] private RectTransform meterMask;
        [SerializeField] private GameObject[] meterShapes = new GameObject[3];
        [SerializeField] private float[] meterHalfHeights = { 135f, 120f, 141f };
        [SerializeField] private LocText noteLabel;
        [SerializeField] private Image countRing;
        [SerializeField] private RectTransform countBadge;
        [SerializeField] private LocText countText;
        [SerializeField] private Image[] beatDots = new Image[4];
        [SerializeField] private AudioSource music;
        [SerializeField] private AudioSource sfx;

        private Vector2 noteHome;
        private double dspStart;
        private double pausedTotal;
        private double pauseStartedAt;
        private bool audioStarted;
        private int lastBeat = int.MinValue;
        private int lastNote = -1;
        private int shownSuccesses = -1;
        private int grade;
        private Coroutine noteRoutine;
        private Coroutine countRoutine;
        private Coroutine ringRoutine;

        public override ScreenId Id => ScreenId.Stage3;
        protected override ScreenId NextScreen => ScreenId.Result;

        protected override void Awake()
        {
            base.Awake();
            noteHome = note.anchoredPosition; // 링과 같은 중심
        }

        protected override StageBase CreateStage()
        {
            return new Stage3Rhythm();
        }

        protected override void BindStage(StageBase s)
        {
            var r = (Stage3Rhythm)s;
            r.NoteAppeared += OnNoteAppeared;
            r.NowReached += OnNowReached;
            r.NoteJudged += OnNoteJudged;
        }

        protected override void UnbindStage(StageBase s)
        {
            var r = (Stage3Rhythm)s;
            r.NoteAppeared -= OnNoteAppeared;
            r.NowReached -= OnNowReached;
            r.NoteJudged -= OnNoteJudged;
        }

        protected override void ResetView()
        {
            lastBeat = int.MinValue;
            lastNote = -1;
            shownSuccesses = -1;
            noteRoutine = null;
            countRoutine = null;
            ringRoutine = null;
            note.gameObject.SetActive(false);
            countBadge.gameObject.SetActive(false);
            SetRing(0f, 1f);
            SetDots(-1, false);
            SetSuccesses(0);
            StartMusic();
        }

        public override void OnExit()
        {
            base.OnExit();
            StopMusic();
        }

        protected override void ConfigureGauge(ForceGauge target)
        {
            // 노트가 나올 때마다 그 등급 구간으로 바뀐다.
            target.ClearZones();
            target.ClearBand();
            target.SetTarget(GaugeTarget.None);
        }

        protected override void GetCompleteContent(StageBase s, out string titleKey, out object titleArg, out int amount)
        {
            titleKey = "s3.clearTitle";
            titleArg = null;
            amount = ((Stage3Rhythm)s).Successes;
        }

        // ---------- 음악 · 시계 ----------

        private void StartMusic()
        {
            EnsureClips();
            music.clip = musicClip;
            music.loop = true;
            music.volume = Config.s3MusicVolume;
            music.mute = !AudioPrefs.SoundEnabled;
            sfx.volume = Config.s3SfxVolume;

            dspStart = AudioSettings.dspTime + ScheduleLeadSeconds;
            pausedTotal = 0d;
            music.PlayScheduled(dspStart);
            audioStarted = true;
        }

        private void StopMusic()
        {
            if (music != null)
                music.Stop();
            audioStarted = false;
        }

        private void EnsureClips()
        {
            if (musicClip == null || !Mathf.Approximately(musicClipBpm, Config.s3Bpm))
            {
                musicClip = ProceduralAudio.NachtmusikLoop(Config.s3Bpm);
                musicClipBpm = Config.s3Bpm;
            }

            if (tickClip == null)
                tickClip = ProceduralAudio.Tick();
            if (nowClip == null)
                nowClip = ProceduralAudio.NowAccent();
            if (goodClip == null)
                goodClip = ProceduralAudio.Good();
            if (missClip == null)
                missClip = ProceduralAudio.Miss();
        }

        /// <summary>음악 재생 위치 = 규칙 시계 (예약 재생 전에는 0).</summary>
        protected override double StageTime()
        {
            if (!audioStarted)
                return 0d;

            double t = AudioSettings.dspTime - dspStart - pausedTotal;
            return t < 0d ? 0d : t;
        }

        protected override void OnPauseChanged(bool isPaused)
        {
            if (!audioStarted)
                return;

            if (isPaused)
            {
                pauseStartedAt = AudioSettings.dspTime;
                music.Pause();
            }
            else
            {
                pausedTotal += AudioSettings.dspTime - pauseStartedAt;
                music.UnPause();
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip != null && AudioPrefs.SoundEnabled)
                sfx.PlayOneShot(clip);
        }

        // ---------- 매 프레임 ----------

        protected override void UpdateView(StageBase s)
        {
            var r = (Stage3Rhythm)s;

            if (r.Successes != shownSuccesses)
                SetSuccesses(r.Successes);

            int beatIndex = r.BeatIndex;
            if (beatIndex != lastBeat || r.NoteIndex != lastNote)
            {
                lastBeat = beatIndex;
                lastNote = r.NoteIndex;
                OnBeat(r, beatIndex);
            }

            switch (r.Phase)
            {
                case Stage3Rhythm.NotePhase.Countdown:
                    if (ringRoutine == null)
                        SetRing(r.CountdownProgress01, 1f);
                    SetMeter(1f); // 카운트다운 동안 노트는 꽉 찬 색
                    break;

                case Stage3Rhythm.NotePhase.Hold:
                    SetMeter(r.MeterProgress01);
                    break;
            }

            bool active = r.Phase == Stage3Rhythm.NotePhase.Countdown || r.Phase == Stage3Rhythm.NotePhase.Hold;
            gauge.SetTarget(active ? GaugeTarget.Band : GaugeTarget.None);
        }

        private void OnBeat(Stage3Rhythm r, int beatIndex)
        {
            int perBar = Mathf.Max(1, Config.s3BeatsPerBar);
            bool holdBar = beatIndex >= perBar;

            if (r.Phase == Stage3Rhythm.NotePhase.LeadIn)
            {
                SetDots(beatIndex % perBar, false);
                return;
            }

            SetDots(beatIndex % perBar, holdBar);

            // 카운트다운 마디: 0박 노트 등장, 1·2·3박 3 · 2 · 1
            if (r.Phase == Stage3Rhythm.NotePhase.Countdown && beatIndex >= 1 && beatIndex < perBar)
            {
                ShowCount(IntStrings.Get(perBar - beatIndex), null);
                PlaySfx(tickClip);
            }
            else if (beatIndex > perBar)
            {
                HideCount();
            }
        }

        private void SetSuccesses(int successes)
        {
            shownSuccesses = successes;
            counterText.SetArgs(successes);
        }

        private void SetDots(int current, bool holdBar)
        {
            for (int i = 0; i < beatDots.Length; i++)
            {
                bool on = i == current;
                Color color = holdBar ? Palette.Green : Palette.Navy;
                color.a = on ? 1f : DotDimAlpha;
                beatDots[i].color = color;
                beatDots[i].rectTransform.localScale = on ? new Vector3(1.3f, 1.3f, 1f) : Vector3.one;
            }
        }

        // ---------- 카운트다운 링 ----------

        private void SetRing(float fill, float alpha)
        {
            countRing.fillAmount = fill;
            Color c = countRing.color;
            c.a = alpha;
            countRing.color = c;
        }

        /// <summary>"지금": 꽉 찬 링이 번쩍 커지며 사라진다.</summary>
        private IEnumerator RingFlashRoutine()
        {
            RectTransform rt = countRing.rectTransform;
            for (float t = 0f; t < RingFlashSeconds; t += Time.deltaTime)
            {
                float k = t / RingFlashSeconds;
                float s = Mathf.Lerp(1f, RingFlashScale, k);
                rt.localScale = new Vector3(s, s, 1f);
                SetRing(1f, 1f - k);
                yield return null;
            }

            rt.localScale = Vector3.one;
            SetRing(0f, 1f);
            ringRoutine = null;
        }

        // ---------- 카운트 배지 ----------

        private void ShowCount(string raw, string key)
        {
            countBadge.gameObject.SetActive(true);
            if (key != null)
                countText.Set(key, null);
            else
                countText.SetRaw(raw);

            if (countRoutine != null)
                StopCoroutine(countRoutine);
            countRoutine = StartCoroutine(PopRoutine(countBadge, CountPop));
        }

        private void HideCount()
        {
            if (countRoutine != null)
                StopCoroutine(countRoutine);
            countRoutine = null;
            countBadge.gameObject.SetActive(false);
        }

        // ---------- 노트 · 버티기 게이지 ----------

        /// <summary>노트 안 버티기 게이지: 아래부터 fraction 만큼 진한 색이 찬다.</summary>
        private void SetMeter(float fraction)
        {
            float half = meterHalfHeights[Mathf.Clamp(grade, 0, meterHalfHeights.Length - 1)];
            meterMask.anchoredPosition = new Vector2(0f, -half);
            meterMask.sizeDelta = new Vector2(meterMask.sizeDelta.x, 2f * half * Mathf.Clamp01(fraction));
        }

        private void OnNoteAppeared(int index, int noteGrade)
        {
            var r = (Stage3Rhythm)Stage;
            grade = noteGrade;
            gauge.SetBand(r.GradeMin(noteGrade), r.GradeMax(noteGrade));

            for (int i = 0; i < noteShapes.Length; i++)
            {
                noteShapes[i].SetActive(i == noteGrade);
                meterShapes[i].SetActive(i == noteGrade);
            }

            noteLabel.Key = Stage3Rhythm.GradeKeys[noteGrade];
            note.gameObject.SetActive(true);
            note.anchoredPosition = noteHome;
            noteGroup.alpha = 1f;
            SetMeter(1f);
            HideCount();

            if (ringRoutine != null)
                StopCoroutine(ringRoutine);
            ringRoutine = null;
            countRing.rectTransform.localScale = Vector3.one;
            SetRing(0f, 1f);

            PlayNote(PopRoutine(note, 1.1f));
        }

        private void OnNowReached(int index)
        {
            ShowCount(null, "s3.now");
            PlaySfx(nowClip);
            SetMeter(0f); // 노트가 연해지고 버티기 게이지가 아래부터 찬다

            if (ringRoutine != null)
                StopCoroutine(ringRoutine);
            ringRoutine = StartCoroutine(RingFlashRoutine());
        }

        private void OnNoteJudged(int index, NoteResult result)
        {
            // "지금" 전에 판정(빨랐어)이 나도 링이 반쯤 찬 채로 남지 않게 비운다.
            if (ringRoutine == null)
            {
                countRing.rectTransform.localScale = Vector3.one;
                SetRing(0f, 1f);
            }

            if (result == NoteResult.Good)
            {
                SetMeter(1f);
                PlaySfx(goodClip);
                PlayNote(SuccessRoutine());
            }
            else
            {
                PlaySfx(missClip);
                PlayNote(FailRoutine());
            }
        }

        private void PlayNote(IEnumerator routine)
        {
            if (noteRoutine != null)
                StopCoroutine(noteRoutine);
            noteRoutine = StartCoroutine(routine);
        }

        /// <summary>톡 튀어나와 살짝 커졌다가 제 크기로.</summary>
        private IEnumerator PopRoutine(RectTransform target, float overshoot)
        {
            float duration = Mathf.Max(0.01f, Config.s3NotePopSeconds);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float s = k < 0.6f ? Mathf.Lerp(0.3f, overshoot, k / 0.6f) : Mathf.Lerp(overshoot, 1f, (k - 0.6f) / 0.4f);
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            target.localScale = Vector3.one;
        }

        /// <summary>성공: 커지며 사라짐 (SPEC §14 노트 성공 0.25초).</summary>
        private IEnumerator SuccessRoutine()
        {
            float duration = Mathf.Max(0.01f, Config.s3NoteSuccessAnimSeconds);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float s = Mathf.Lerp(1f, SuccessScale, k);
                note.localScale = new Vector3(s, s, 1f);
                noteGroup.alpha = 1f - k;
                yield return null;
            }

            note.gameObject.SetActive(false);
            noteRoutine = null;
        }

        /// <summary>실패: 좌우로 흔들리며 흐려짐.</summary>
        private IEnumerator FailRoutine()
        {
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                note.anchoredPosition = noteHome + new Vector2(ShakePixels * Mathf.Sin(k * Mathf.PI * 6f) * (1f - k), 0f);
                noteGroup.alpha = Mathf.Lerp(1f, FailAlpha, k);
                yield return null;
            }

            note.anchoredPosition = noteHome;
            noteRoutine = null;
        }
    }
}
