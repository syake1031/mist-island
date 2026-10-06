using System;

namespace MistIsland
{
    /// <summary>成長・コスト・難易度の計算式。数値そのものは GameConfig に置く。</summary>
    public static class Formulas
    {
        /// <summary>level から level+1 に上がるのに必要な経験値。</summary>
        public static int XpToNext(int level, int xpToLevel2, float growth)
        {
            return (int)Math.Round(xpToLevel2 * Math.Pow(growth, level - 1));
        }

        /// <summary>現在 level のものを1段階上げる（または level=0 から作る）コスト。</summary>
        public static int UpgradeCost(int baseCost, float growth, int level)
        {
            if (baseCost <= 0) return 0;
            return (int)Math.Ceiling(baseCost * Math.Pow(growth, Math.Max(0, level - 1)));
        }

        /// <summary>日数に比例して強くなる倍率（1日目 = 1）。</summary>
        public static float DayMultiplier(int day, float growthPerDay)
        {
            return 1f + growthPerDay * Math.Max(0, day - 1);
        }

        public static float SpawnInterval(int day, float baseInterval, float minInterval, float dayFactor)
        {
            return Math.Max(minInterval, baseInterval / (1f + dayFactor * Math.Max(0, day - 1)));
        }

        /// <summary>放置中の夜の敵の強さ。防衛力がこれ以上なら持ちこたえる。</summary>
        public static float NightStrength(int day, float baseStrength, float growthPerDay)
        {
            return (float)(baseStrength * Math.Pow(growthPerDay, Math.Max(0, day - 1)));
        }

        /// <summary>放置中に夜を持ちこたえたときに手に入る素材。</summary>
        public static int OfflineNightMaterials(int day, float basePerNight, float perDay)
        {
            return (int)Math.Floor(basePerNight + perDay * Math.Max(0, day - 1));
        }
    }
}
