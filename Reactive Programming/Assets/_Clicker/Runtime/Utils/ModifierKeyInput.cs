using UnityEngine.InputSystem;

namespace Utils {
    public static class ModifierKeyInput {

        public static bool IsControlPressed {
            get {
                var keyboard = Keyboard.current;
                return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            }
        }

        public static bool IsShiftPressed {
            get {
                var keyboard = Keyboard.current;
                return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            }
        }
    }
}
