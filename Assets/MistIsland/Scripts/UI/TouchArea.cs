using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MistIsland
{
    /// <summary>
    /// 画面全体のタッチ受付（ボタンの後ろ側）。
    /// ・左半分：押したところに仮想スティックが出て移動
    /// ・右半分：横にドラッグするとカメラが回る。動かさずに長押しすると溜め攻撃
    /// 指ごとに pointerId で区別するので、移動しながら回転・溜めもできる。
    /// </summary>
    public class TouchArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float joystickRadius = 120f;
        public float rotateDegreesPerPixel = 0.25f;
        /// <summary>これ以上動いたら長押しではなくドラッグとみなす（画面ピクセル）。</summary>
        public float holdTolerance = 30f;

        class RightPointer
        {
            public Vector2 start;
            public float downTime;
            public bool rotating;
            public bool charging;
        }

        RectTransform _rect;
        RectTransform _stickBase;
        RectTransform _stickKnob;
        int _stickPointer = int.MinValue;
        Vector2 _stickOrigin;
        readonly Dictionary<int, RightPointer> _right = new Dictionary<int, RightPointer>();

        public void Initialize(RectTransform stickBase, RectTransform stickKnob)
        {
            _rect = (RectTransform)transform;
            _stickBase = stickBase;
            _stickKnob = stickKnob;
            _stickBase.gameObject.SetActive(false);
        }

        Vector2 ToLocal(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out local);
            return local;
        }

        public void OnPointerDown(PointerEventData e)
        {
            bool leftHalf = e.position.x < Screen.width * 0.5f;
            if (leftHalf && _stickPointer == int.MinValue)
            {
                _stickPointer = e.pointerId;
                _stickOrigin = ToLocal(e);
                _stickBase.anchoredPosition = _stickOrigin;
                _stickKnob.anchoredPosition = Vector2.zero;
                _stickBase.gameObject.SetActive(true);
                InputBridge.JoystickValue = Vector2.zero;
            }
            else
            {
                _right[e.pointerId] = new RightPointer { start = e.position, downTime = Time.unscaledTime };
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _stickPointer)
            {
                Vector2 offset = ToLocal(e) - _stickOrigin;
                Vector2 clamped = Vector2.ClampMagnitude(offset, joystickRadius);
                _stickKnob.anchoredPosition = clamped;
                Vector2 v = clamped / joystickRadius;
                // 小さな揺れは無視する
                InputBridge.JoystickValue = v.magnitude < 0.12f ? Vector2.zero : v;
                return;
            }

            RightPointer p;
            if (!_right.TryGetValue(e.pointerId, out p) || p.charging) return;
            if (!p.rotating && (e.position - p.start).magnitude > holdTolerance) p.rotating = true;
            if (p.rotating)
                InputBridge.PendingCameraYaw += e.delta.x * rotateDegreesPerPixel * (1080f / Mathf.Max(1f, Screen.width));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _stickPointer)
            {
                _stickPointer = int.MinValue;
                _stickBase.gameObject.SetActive(false);
                InputBridge.JoystickValue = Vector2.zero;
            }
            _right.Remove(e.pointerId);
            RefreshCharge();
        }

        void Update()
        {
            // 動かさずに押し続けている指があれば溜め開始
            float delay = GameConfig.Load().chargeStartDelay;
            foreach (var p in _right.Values)
            {
                if (!p.rotating && !p.charging && Time.unscaledTime - p.downTime >= delay)
                    p.charging = true;
            }
            RefreshCharge();
        }

        void RefreshCharge()
        {
            bool charging = false;
            foreach (var p in _right.Values)
                if (p.charging) charging = true;
            InputBridge.ChargeHeld = charging;
        }

        void OnDisable()
        {
            _stickPointer = int.MinValue;
            _right.Clear();
            InputBridge.JoystickValue = Vector2.zero;
            InputBridge.ChargeHeld = false;
            if (_stickBase != null) _stickBase.gameObject.SetActive(false);
        }
    }
}
