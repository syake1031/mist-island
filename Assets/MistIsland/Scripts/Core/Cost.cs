using System;
using System.Text;

namespace MistIsland
{
    /// <summary>コインと素材の組み合わせ（建設・作成・強化のコストや、敵の落とし物）。</summary>
    [Serializable]
    public class Cost
    {
        public int coins;
        public int wood;
        public int stone;
        public int iron;
        public int crystal;

        public static readonly ResourceType[] All =
        {
            ResourceType.Coins, ResourceType.Wood, ResourceType.Stone, ResourceType.Iron, ResourceType.Crystal,
        };

        public int Get(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Coins: return coins;
                case ResourceType.Wood: return wood;
                case ResourceType.Stone: return stone;
                case ResourceType.Iron: return iron;
                default: return crystal;
            }
        }

        public void Set(ResourceType type, int value)
        {
            switch (type)
            {
                case ResourceType.Coins: coins = value; break;
                case ResourceType.Wood: wood = value; break;
                case ResourceType.Stone: stone = value; break;
                case ResourceType.Iron: iron = value; break;
                default: crystal = value; break;
            }
        }

        public bool IsFree
        {
            get { return coins <= 0 && wood <= 0 && stone <= 0 && iron <= 0 && crystal <= 0; }
        }

        /// <summary>各値を倍率でかけて切り上げる（0 は 0 のまま）。</summary>
        public Cost Scaled(double factor)
        {
            var c = new Cost();
            foreach (var t in All)
            {
                int v = Get(t);
                c.Set(t, v > 0 ? (int)Math.Ceiling(v * factor) : 0);
            }
            return c;
        }

        public string ToText()
        {
            if (IsFree) return "無料";
            var sb = new StringBuilder();
            foreach (var t in All)
            {
                int v = Get(t);
                if (v <= 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                if (t == ResourceType.Coins) sb.Append(v).Append("コイン");
                else sb.Append(Names.Short(t)).Append(v);
            }
            return sb.ToString();
        }

        public static Cost Of(int coins, int wood = 0, int stone = 0, int iron = 0, int crystal = 0)
        {
            return new Cost { coins = coins, wood = wood, stone = stone, iron = iron, crystal = crystal };
        }
    }
}
