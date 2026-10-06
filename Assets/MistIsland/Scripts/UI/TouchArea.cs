using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MistIsland
{
    /// <summary>
    /// 画面全体のタッチ受付（ボタンの後ろ側）。
    /// 左半分を押すとその場に仮想スティックが出て移動、右半分を横にドラッグするとカメラが回る。
    /// 指ごとに pointerId で区別するので、移動しながら回転もできる。
    /// </summary>
    public class TouchArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float joystickRadius = 120f;
        public float rotateDegreesPerPixel = 0.25f;

        RectTransform _rect;
        RectTransform _stickBase;
        RectTransform _stickKnob;
        int _stickPointer = int.MinValue;
        Vector2 _stickOrigin;
        readonly HashSet<int> _rotatePointers = new HashSet<int>();

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
                _rotatePointers.Add(e.pointerId);
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
            }
            else if (_rotatePointers.Contains(e.pointerId))
            {
                InputBridge.PendingCameraYaw += e.delta.x * rotateDegreesPerPixel * (1080f / Mathf.Max(1f, Screen.width));
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _stickPointer)
            {
                _stickPointer = int.MinValue;
                _stickBase.gameObject.SetActive(false);
                InputBridge.JoystickValue = Vector2.zero;
            }
            _rotatePointers.Remove(e.pointerId);
        }

        void OnDisable()
        {
            _stickPointer = int.MinValue;
            _rotatePointers.Clear();
            InputBridge.JoystickValue = Vector2.zero;
            if (_stickBase != null) _stickBase.gameObject.SetActive(false);
        }
    }
}
