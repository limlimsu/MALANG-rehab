namespace Malang.Training
{
    /// <summary>
    /// 단계 공통: 시작 시각, 프레임 간격, 아동에게 보여 줄 안내 문구(Loc 키 + 인자).
    /// 화면은 MessageVersion 이 바뀔 때만 문구를 갱신한다.
    /// </summary>
    public abstract class StageBase : IStage
    {
        private double startTime = -1d;

        protected StageConfig Cfg { get; private set; }
        protected double Now { get; private set; }
        protected float Dt { get; private set; }
        protected bool Started => startTime >= 0d;
        protected double Elapsed => Started ? Now - startTime : 0d;

        public bool IsDone { get; protected set; }

        public string MessageKey { get; private set; }
        public object MessageArg { get; private set; }
        public int MessageVersion { get; private set; }

        public void Begin(StageConfig cfg)
        {
            Cfg = cfg;
            startTime = -1d;
            Now = 0d;
            Dt = 0f;
            IsDone = false;
            OnBegin();
        }

        public void Tick(float grip, double t)
        {
            if (!Started)
            {
                startTime = t;
                Now = t;
                OnStart(t);
            }

            Dt = (float)(t - Now);
            Now = t;
            OnTick(grip, t);
        }

        public StageResult End()
        {
            return BuildResult();
        }

        /// <summary>시연·개발용 즉시 완료 (F2). 점수는 그대로 둔다.</summary>
        public void ForceComplete()
        {
            if (!IsDone)
                Finish();
        }

        protected abstract void OnBegin();
        protected abstract void OnStart(double t);
        protected abstract void OnTick(float grip, double t);
        protected abstract void Finish();
        protected abstract StageResult BuildResult();

        protected void SetMessage(string key, object arg = null)
        {
            if (key == MessageKey && Equals(arg, MessageArg))
                return;

            MessageKey = key;
            MessageArg = arg;
            MessageVersion++;
        }
    }
}
