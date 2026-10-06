using System.Collections.Generic;
using UnityEngine;

namespace MistIsland
{
    public struct SlotInfo
    {
        public int id;
        public Vector3 position;
        /// <summary>外向きの向き（度）。柵などの向きに使う。</summary>
        public float yaw;
    }

    /// <summary>
    /// 建物を置ける空き地。細かい格子の上で台地の平らなところを探し、
    /// 拠点に近い順に、建物どうしが重ならない間隔で選ぶ。
    /// ID は格子の番号から決まるので、セーブデータとずれない。
    /// </summary>
    public static class TownLayout
    {
        public const float HallRadius = 2.8f;
        /// <summary>建物1つぶんの広さ（空き地どうしの最小間隔）。</summary>
        public const float SlotSpacing = 5f;
        const float Step = 1.5f;
        const int Offset = 200;

        public static SlotInfo FromId(int id)
        {
            int gx = id / 1000 - Offset;
            int gz = id % 1000 - Offset;
            var p = new Vector3(gx * Step, 0f, gz * Step);
            return new SlotInfo
            {
                id = id,
                position = p,
                yaw = p.sqrMagnitude > 0.01f ? Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg : 0f,
            };
        }

        static int ToId(int gx, int gz)
        {
            return (gx + Offset) * 1000 + (gz + Offset);
        }

        /// <summary>今の島で使える空き地（高さ付き）。海岸の近く・崖・拠点のそばは除く。</summary>
        public static List<SlotInfo> AvailableSlots(Island island)
        {
            var candidates = new List<SlotInfo>();
            int n = Mathf.CeilToInt(island.Radius * 1.2f / Step);
            for (int gx = -n; gx <= n; gx++)
            {
                for (int gz = -n; gz <= n; gz++)
                {
                    SlotInfo s = FromId(ToId(gx, gz));
                    float x = s.position.x, z = s.position.z;
                    if (s.position.magnitude < HallRadius + 2.5f) continue;
                    if (island.NormalizedDistance(x, z) > 0.8f) continue;
                    float h = island.HeightAt(x, z);
                    if (h < 0.5f) continue;
                    // 建物の足元が平らなところだけ（崖の途中には置かない）
                    if (island.SlopeAt(x, z, 1.6f) > 0.18f) continue;
                    s.position.y = h;
                    candidates.Add(s);
                }
            }

            // 拠点に近い順に、重ならないように選ぶ
            candidates.Sort((a, b) => a.position.sqrMagnitude.CompareTo(b.position.sqrMagnitude));
            var picked = new List<SlotInfo>();
            foreach (var c in candidates)
            {
                if (!IsNearSlot(picked, c.position, SlotSpacing, false)) picked.Add(c);
            }
            return picked;
        }

        public static bool IsNearSlot(List<SlotInfo> slots, Vector3 p, float distance, bool includeHall = true)
        {
            p.y = 0f;
            if (includeHall && p.magnitude < HallRadius + distance) return true;
            if (slots == null) return false;
            foreach (var s in slots)
            {
                Vector3 d = s.position - p;
                d.y = 0f;
                if (d.sqrMagnitude < distance * distance) return true;
            }
            return false;
        }
    }
}
