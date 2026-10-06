using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// 斜め上からプレイヤーを追うカメラ。画面右半分のドラッグや Q/E で回転できる。
    /// 縦画面なので、視野角は狭め・距離は遠めにしてジオラマ風に見せる。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public float pitch = 48f;
        public float distance = 44f;
        public float minDistance = 26f;
        public float maxDistance = 70f;
        public float fieldOfView = 36f;
        public float followSharpness = 6f;
        public float keyboardRotateSpeed = 90f;

        public float Yaw { get; private set; }

        Transform _target;
        Camera _camera;
        Vector3 _focus;
        float _targetYaw;

        public Camera Camera { get { return _camera; } }

        public void Initialize(Camera cam, Transform target)
        {
            _camera = cam;
            _target = target;
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 250f;
            Yaw = _targetYaw = 45f;
            _focus = target != null ? target.position : Vector3.zero;
            Place();
        }

        /// <summary>カメラの向きに合わせて、スティック入力をワールドの方向に直す。</summary>
        public Vector3 ToWorldDirection(Vector2 input)
        {
            Quaternion rot = Quaternion.Euler(0f, Yaw, 0f);
            return rot * new Vector3(input.x, 0f, input.y);
        }

        void LateUpdate()
        {
            if (_camera == null) return;

            _targetYaw += InputBridge.ConsumeCameraYaw(keyboardRotateSpeed);
            Yaw = Mathf.LerpAngle(Yaw, _targetYaw, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

            float zoom = InputBridge.ConsumeZoom();
            if (Mathf.Abs(zoom) > 0.0001f)
                distance = Mathf.Clamp(distance - zoom * 3f, minDistance, maxDistance);

            if (_target != null)
                _focus = Vector3.Lerp(_focus, _target.position, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            Place();
        }

        void Place()
        {
            Quaternion rot = Quaternion.Euler(pitch, Yaw, 0f);
            Vector3 focus = _focus + Vector3.up * 0.8f;
            _camera.transform.position = focus - rot * Vector3.forward * distance;
            _camera.transform.rotation = rot;
        }
    }
}
