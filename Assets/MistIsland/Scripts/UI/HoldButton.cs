using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MistIsland
{
    /// <summary>押している間だけ有効になるボタン（攻撃ボタン）。</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action<bool> Changed;
        Image _image;
        Color _normal;
        int _pointers;

        void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null) _normal = _image.color;
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointers++;
            Set(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            _pointers = Mathf.Max(0, _pointers - 1);
            if (_pointers == 0) Set(false);
        }

        void OnDisable()
        {
            _pointers = 0;
            Set(false);
        }

        void Set(bool held)
        {
            if (_image != null) _image.color = held ? _normal * 0.8f : _normal;
            if (Changed != null) Changed(held);
        }
    }
}
