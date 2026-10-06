using UnityEngine;

namespace MistIsland
{
    /// <summary>体力。プレイヤー・敵・建物で共通。</summary>
    public class Health : MonoBehaviour
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        /// <summary>当たり判定や攻撃の届く距離に使う半径。</summary>
        public float Radius = 0.5f;
        /// <summary>受けるダメージの割合を減らす（防具）。0..1</summary>
        public float DamageReduction;

        public bool IsAlive { get { return Current > 0f; } }
        public float Ratio { get { return Max > 0f ? Current / Max : 0f; } }

        public event System.Action<float> Damaged;
        public event System.Action Died;

        public void Init(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        /// <summary>最大値を変える。割合を保つ。</summary>
        public void SetMax(float max)
        {
            float ratio = Max > 0f ? Current / Max : 1f;
            Max = Mathf.Max(1f, max);
            if (Current > 0f) Current = Mathf.Max(1f, Max * ratio);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            amount *= 1f - Mathf.Clamp01(DamageReduction);
            Current = Mathf.Max(0f, Current - amount);
            if (Damaged != null) Damaged(amount);
            if (Current <= 0f && Died != null) Died();
        }

        public void HealFull()
        {
            Current = Max;
        }

        public Vector3 Position { get { return transform.position; } }
    }
}
