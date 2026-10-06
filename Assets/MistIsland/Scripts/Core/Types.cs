namespace MistIsland
{
    public enum Phase { Morning = 0, Day = 1, Night = 2 }

    public enum WeaponType { Sword = 0, Spear = 1, Bow = 2 }

    public enum BuildingType { Bank = 0, Farm = 1, Mine = 2, Watchtower = 3, Fence = 4 }

    /// <summary>
    /// 放置中に防衛装置が壊されたときの報酬の扱い（企画書の未決事項）。
    /// GameConfig で切り替えられるようにしておき、実装を確かめながら決める。
    /// </summary>
    public enum OfflineBreachRule
    {
        /// <summary>壊された場合、その放置期間の報酬はすべて失う。</summary>
        LoseAll = 0,
        /// <summary>壊されるまでに貯まった分は受け取れる。</summary>
        KeepUntilBreach = 1,
    }

    public static class Names
    {
        public static string Of(Phase phase)
        {
            switch (phase)
            {
                case Phase.Morning: return "朝";
                case Phase.Day: return "昼";
                default: return "夜";
            }
        }

        public static string Of(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Sword: return "剣";
                case WeaponType.Spear: return "槍";
                default: return "弓";
            }
        }
    }
}
