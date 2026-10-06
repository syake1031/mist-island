using System.Collections.Generic;
using UnityEngine;

namespace MistIsland
{
    public struct SlotInfo
    {
        public int id;
        public int ring;
        public Vector3 position;
        /// <summary>外向きの向き（度）。柵などの向きに使う。</summary>
        public float yaw;
    }

    /// <summary>
    /// 建物を置ける空き地の配置。町の中心を囲む輪の上に並び、島を拡張するたびに外側の輪が使えるようになる。
    /// ID は輪と番号から決まるので、島の大きさが変わってもセーブデータとずれない。
    /// </summary>
    public static class TownLayout
    {
        public const float HallRadius = 1.8f;
        const float FirstRing = 5.5f;
        const float RingSpacing = 4f;
        const float SlotSpacing = 4.6f;
        const int MaxRings = 10;

        static List<SlotInfo> _all;

        public static float RingRadius(int ring)
        {
            return FirstRing + RingSpacing * ring;
        }

        public static bool RingAvailable(int ring, float islandRadius)
        {
            return RingRadius(ring) <= islandRadius - 4f;
        }

        static List<SlotInfo> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<SlotInfo>();
                for (int ring = 0; ring < MaxRings; ring++)
                {
                    float r = RingRadius(ring);
                    int count = Mathf.Max(4, Mathf.FloorToInt(2f * Mathf.PI * r / SlotSpacing));
                    float offset = (ring % 2) * 0.5f;
                    for (int j = 0; j < count; j++)
                    {
                        float a = (j + offset) / count * Mathf.PI * 2f;
                        _all.Add(new SlotInfo
                        {
                            id = ring * 100 + j,
                            ring = ring,
                            position = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                            yaw = 90f - a * Mathf.Rad2Deg,
                        });
                    }
                }
                return _all;
            }
        }

        /// <summary>今の島で使える空き地（高さ付き）。海岸に近すぎるものは除く。</summary>
        public static List<SlotInfo> AvailableSlots(Island island)
        {
            var list = new List<SlotInfo>();
            foreach (var s in All)
            {
                if (!RingAvailable(s.ring, island.Radius)) continue;
                if (island.NormalizedDistance(s.position.x, s.position.z) > 0.82f) continue;
                float h = island.HeightAt(s.position.x, s.position.z);
                if (h < 0.4f) continue;
                var slot = s;
                slot.position.y = h;
                list.Add(slot);
            }
            return list;
        }

        public static bool IsNearAnySlot(Vector3 p, float distance, float islandRadius)
        {
            p.y = 0f;
            if (p.magnitude < HallRadius + distance) return true;
            foreach (var s in All)
            {
                if (!RingAvailable(s.ring, islandRadius)) continue;
                Vector3 d = s.position - p;
                d.y = 0f;
                if (d.sqrMagnitude < distance * distance) return true;
            }
            return false;
        }
    }
}
