using UnityEngine;

public static class GARDENINPUT
{
    public static bool InteractPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }
    }

    public static bool CancelPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }

    public static Vector2 Move
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f)
                    - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f);
            float y = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f)
                    - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);
            return new Vector2(x, y);
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }
    }
}