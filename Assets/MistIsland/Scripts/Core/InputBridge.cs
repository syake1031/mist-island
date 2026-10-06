using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MistIsland
{
    /// <summary>
    /// 画面タッチ（仮想スティック・ボタン）とキーボードの入力をまとめる。
    /// タッチ側は UI が値を書き込み、ゲーム側はここだけを読む。
    /// キーボード：WASD/矢印 移動、Space 攻撃、F 調べる、Q/E カメラ回転、ホイール ズーム
    /// </summary>
    public static class InputBridge
    {
        public static Vector2 JoystickValue;
        public static bool AttackHeld;
        public static float PendingCameraYaw;
        public static float PendingZoom;

        public static Vector2 Move
        {
            get
            {
                Vector2 v = JoystickValue + KeyboardMove();
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static bool Attack
        {
            get { return AttackHeld || KeyHeld(KeyKind.Attack); }
        }

        public static bool InteractPressed
        {
            get { return KeyDown(KeyKind.Interact); }
        }

        public static bool MenuPressed
        {
            get { return KeyDown(KeyKind.Menu); }
        }

        /// <summary>このフレームのカメラ回転量（度）。読み出すと UI からの分はリセットされる。</summary>
        public static float ConsumeCameraYaw(float keyboardDegreesPerSecond)
        {
            float yaw = PendingCameraYaw;
            PendingCameraYaw = 0f;
            float axis = 0f;
            if (KeyHeld(KeyKind.RotateLeft)) axis -= 1f;
            if (KeyHeld(KeyKind.RotateRight)) axis += 1f;
            return yaw + axis * keyboardDegreesPerSecond * Time.unscaledDeltaTime;
        }

        public static float ConsumeZoom()
        {
            float z = PendingZoom;
            PendingZoom = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) z += Mouse.current.scroll.ReadValue().y / 120f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            z += Input.mouseScrollDelta.y;
#endif
            return z;
        }

        enum KeyKind { Attack, Interact, Menu, RotateLeft, RotateRight }

        static Vector2 KeyboardMove()
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return v;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1f;
#endif
            return v;
        }

        static bool KeyHeld(KeyKind kind)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (kind)
            {
                case KeyKind.Attack: return kb.spaceKey.isPressed;
                case KeyKind.Interact: return kb.fKey.isPressed;
                case KeyKind.Menu: return kb.tabKey.isPressed;
                case KeyKind.RotateLeft: return kb.qKey.isPressed;
                case KeyKind.RotateRight: return kb.eKey.isPressed;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(LegacyKey(kind));
#else
            return false;
#endif
        }

        static bool KeyDown(KeyKind kind)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (kind)
            {
                case KeyKind.Attack: return kb.spaceKey.wasPressedThisFrame;
                case KeyKind.Interact: return kb.fKey.wasPressedThisFrame;
                case KeyKind.Menu: return kb.tabKey.wasPressedThisFrame;
                case KeyKind.RotateLeft: return kb.qKey.wasPressedThisFrame;
                case KeyKind.RotateRight: return kb.eKey.wasPressedThisFrame;
            }
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(LegacyKey(kind));
#else
            return false;
#endif
        }

#if !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
        static KeyCode LegacyKey(KeyKind kind)
        {
            switch (kind)
            {
                case KeyKind.Attack: return KeyCode.Space;
                case KeyKind.Interact: return KeyCode.F;
                case KeyKind.Menu: return KeyCode.Tab;
                case KeyKind.RotateLeft: return KeyCode.Q;
                default: return KeyCode.E;
            }
        }
#endif

        public static void Reset()
        {
            JoystickValue = Vector2.zero;
            AttackHeld = false;
            PendingCameraYaw = 0f;
            PendingZoom = 0f;
        }
    }
}
