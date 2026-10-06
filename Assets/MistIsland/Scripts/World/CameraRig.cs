using UnityEngine;

namespace MistIsland
{
    /// <summary>
    /// 斜め上から島を見下ろすカメラ。島全体が一画面に収まる距離に自動で合わせ、
    /// プレイヤーの方へ少しだけ寄せる。画面右半分のドラッグや Q/E で回転、ホイールでズーム。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public float pitch = 52f;
        public float fieldOfView = 40f;
        [Tooltip("島の端に残す余白")]
        public float margin = 1.5f;
        [Tooltip("どれだけプレイヤーの方へ寄せるか（0 で島の中心固定、1 でプレイヤー中心）")]
        public float followWeight = 0.3f;
        public float followSharpness = 6f;
        public float keyboardRotateSpeed = 90f;
        public float minZoom = 0.5f;
        public float maxZoom = 1.4f;

        /// <summary>画面に収める島の半径。</summary>
        public float FitRadius = 13f;
        /// <summary>今のカメラの距離（霧の計算でも使う）。</summary>
        public float distance = 40f;

        public float Yaw { get; private set; }

        Transform _target;
        Camera _camera;
        Vector3 _focus;
        float _targetYaw;
        float _zoom = 1f;

        public Camera Camera { get { return _camera; } }

        public void Initialize(Camera cam, Transform target)
        {
            _camera = cam;
            _target = target;
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 250f;
            Yaw = _targetYaw = 45f;
            _focus = DesiredFocus();
            Place();
        }

        /// <summary>カメラの向きに合わせて、スティック入力をワールドの方向に直す。</summary>
        public Vector3 ToWorldDirection(Vector2 input)
        {
            Quaternion rot = Quaternion.Euler(0f, Yaw, 0f);
            return rot * new Vector3(input.x, 0f, input.y);
        }

        /// <summary>島の直径が画面の横幅に収まる距離。</summary>
        float FitDistance()
        {
            float halfV = fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanH = Mathf.Tan(halfV) * Mathf.Max(0.3f, _camera.aspect);
            return (FitRadius + margin) / Mathf.Max(0.05f, tanH);
        }

        Vector3 DesiredFocus()
        {
            Vector3 center = new Vector3(0f, 1.5f, 0f);
            return _target != null ? Vector3.Lerp(center, _target.position, followWeight) : center;
        }

        void LateUpdate()
        {
            if (_camera == null) return;

            _targetYaw += InputBridge.ConsumeCameraYaw(keyboardRotateSpeed);
            Yaw = Mathf.LerpAngle(Yaw, _targetYaw, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

            float zoom = InputBridge.ConsumeZoom();
            if (Mathf.Abs(zoom) > 0.0001f)
                _zoom = Mathf.Clamp(_zoom - zoom * 0.08f, minZoom, maxZoom);

            _focus = Vector3.Lerp(_focus, DesiredFocus(), 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
            Place();
        }

        void Place()
        {
            _camera.fieldOfView = fieldOfView;
            distance = FitDistance() * _zoom;
            Quaternion rot = Quaternion.Euler(pitch, Yaw, 0f);
            _camera.transform.position = _focus - rot * Vector3.forward * distance;
            _camera.transform.rotation = rot;
        }
    }
}
