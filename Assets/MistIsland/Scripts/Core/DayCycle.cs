using UnityEngine;

namespace MistIsland
{
    /// <summary>朝3分・昼3分・夜3分の時間サイクル。長さは GameConfig で変える。</summary>
    public class DayCycle : MonoBehaviour
    {
        public int Day { get; private set; }
        public float Time { get; private set; }
        public Phase Phase { get; private set; }

        public event System.Action<Phase> PhaseChanged;
        public event System.Action<int> DayStarted;

        DayDurations _durations;

        public DayDurations Durations { get { return _durations; } }
        public float PhaseElapsed { get { return Time - _durations.Start(Phase); } }
        public float PhaseRemaining { get { return _durations.End(Phase) - Time; } }
        public float PhaseProgress { get { return Mathf.Clamp01(PhaseElapsed / Mathf.Max(0.001f, _durations.Length(Phase))); } }
        public bool IsNight { get { return Phase == Phase.Night; } }

        public void Initialize(DayDurations durations, int day, float time)
        {
            _durations = durations;
            Day = Mathf.Max(1, day);
            Time = Mathf.Clamp(time, 0f, Mathf.Max(0f, durations.Total - 0.001f));
            Phase = _durations.PhaseAt(Time);
        }

        void Update()
        {
            Advance(UnityEngine.Time.deltaTime);
        }

        public void Advance(float seconds)
        {
            Time += seconds;
            while (Time >= _durations.Total)
            {
                Time -= _durations.Total;
                Day++;
                if (DayStarted != null) DayStarted(Day);
            }
            RefreshPhase();
        }

        /// <summary>放置計算の結果などで時刻を直接変える。時間帯が変われば通知する。</summary>
        public void SetState(int day, float time)
        {
            bool newDay = day != Day;
            Day = Mathf.Max(1, day);
            Time = Mathf.Clamp(time, 0f, Mathf.Max(0f, _durations.Total - 0.001f));
            if (newDay && DayStarted != null) DayStarted(Day);
            RefreshPhase();
        }

        /// <summary>テスト用：次の時間帯の始まりまで飛ばす。</summary>
        public void SkipToNextPhase()
        {
            Advance(PhaseRemaining + 0.001f);
        }

        void RefreshPhase()
        {
            Phase phase = _durations.PhaseAt(Time);
            if (phase == Phase) return;
            Phase = phase;
            if (PhaseChanged != null) PhaseChanged(phase);
        }
    }
}
