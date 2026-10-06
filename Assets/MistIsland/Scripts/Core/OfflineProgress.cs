using System;

namespace MistIsland
{
    public struct OfflineInput
    {
        public DayDurations Durations;
        public int Day;
        public double CycleTime;
        public double ElapsedSeconds;
        public double MaxSeconds;

        /// <summary>拠点の防衛装置レベルの合計（防衛力）。</summary>
        public float DefensePower;
        public float EnemyBaseStrength;
        public float EnemyGrowthPerDay;
        public float MaterialsPerNightBase;
        public float MaterialsPerDay;
        public OfflineBreachRule Rule;
    }

    public struct OfflineResult
    {
        public int Day;
        public double CycleTime;
        /// <summary>実際に時間を進めた秒数（上限で切り詰めた後）。</summary>
        public double SimulatedSeconds;
        /// <summary>施設の収入として数える秒数。壊されると減る。</summary>
        public double ProductiveSeconds;
        public int NightsSurvived;
        public int Materials;
        public bool Breached;
        public int BreachedOnDay;
    }

    /// <summary>
    /// アプリを閉じていた間の進行を計算する。
    /// 夜に入るたびに「防衛力」と「その夜の敵の強さ」を比べ、
    /// 持ちこたえれば収入と素材を得る。壊されたら以降の収入は止まり、
    /// OfflineBreachRule.LoseAll のときはその放置期間の報酬がすべてなくなる。
    /// </summary>
    public static class OfflineProgress
    {
        public static OfflineResult Simulate(OfflineInput input)
        {
            var result = new OfflineResult
            {
                Day = Math.Max(1, input.Day),
                CycleTime = input.CycleTime,
            };

            DayDurations d = input.Durations;
            double total = d.Total;
            if (total <= 0 || input.ElapsedSeconds <= 0) return result;

            double remaining = Math.Min(input.ElapsedSeconds, Math.Max(0, input.MaxSeconds));
            result.SimulatedSeconds = remaining;

            int day = result.Day;
            double t = Math.Max(0, Math.Min(input.CycleTime, total));
            bool breached = false;

            // 1日あたり最大3区間なので、上限時間が長くてもループ回数は小さい
            int guard = 0;
            while (remaining > 1e-9 && guard++ < 1000000)
            {
                if (t >= total)
                {
                    t = 0;
                    day++;
                }

                Phase phase = d.PhaseAt(t);
                double end = d.End(phase);
                double step;
                bool reachedEnd;
                if (remaining >= end - t)
                {
                    step = end - t;
                    t = end;
                    reachedEnd = true;
                }
                else
                {
                    step = remaining;
                    t += step;
                    reachedEnd = false;
                }
                remaining -= step;

                if (phase == Phase.Night && step > 0 && !breached)
                {
                    float strength = Formulas.NightStrength(day, input.EnemyBaseStrength, input.EnemyGrowthPerDay);
                    if (input.DefensePower >= strength)
                    {
                        if (reachedEnd)
                        {
                            result.NightsSurvived++;
                            result.Materials += Formulas.OfflineNightMaterials(day, input.MaterialsPerNightBase, input.MaterialsPerDay);
                        }
                    }
                    else
                    {
                        breached = true;
                        result.Breached = true;
                        result.BreachedOnDay = day;
                    }
                }

                if (!breached) result.ProductiveSeconds += step;
            }

            if (t >= total)
            {
                t = 0;
                day++;
            }

            if (breached && input.Rule == OfflineBreachRule.LoseAll)
            {
                result.ProductiveSeconds = 0;
                result.Materials = 0;
            }

            result.Day = day;
            result.CycleTime = t;
            return result;
        }
    }
}
