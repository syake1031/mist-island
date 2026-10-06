namespace MistIsland
{
    /// <summary>
    /// 1日（朝・昼・夜）の長さ。時刻 t は 1日の始まり（朝の開始）からの経過秒。
    /// UnityEngine に依存しないので、放置計算やテストからも使える。
    /// </summary>
    public struct DayDurations
    {
        public float Morning;
        public float Day;
        public float Night;

        public DayDurations(float morning, float day, float night)
        {
            Morning = morning;
            Day = day;
            Night = night;
        }

        public float Total { get { return Morning + Day + Night; } }

        public Phase PhaseAt(double t)
        {
            if (t < Morning) return Phase.Morning;
            if (t < Morning + Day) return Phase.Day;
            return Phase.Night;
        }

        public float Length(Phase phase)
        {
            switch (phase)
            {
                case Phase.Morning: return Morning;
                case Phase.Day: return Day;
                default: return Night;
            }
        }

        public float Start(Phase phase)
        {
            switch (phase)
            {
                case Phase.Morning: return 0f;
                case Phase.Day: return Morning;
                default: return Morning + Day;
            }
        }

        public float End(Phase phase)
        {
            return Start(phase) + Length(phase);
        }
    }
}
