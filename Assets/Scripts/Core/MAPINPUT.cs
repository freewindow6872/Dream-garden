using UnityEngine;

public static class MAPINPUT
{
#if ENABLE_INPUT_SYSTEM
    static bool P(UnityEngine.InputSystem.Key a, UnityEngine.InputSystem.Key b)
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame);
    }

    public static bool Left  { get { return P(UnityEngine.InputSystem.Key.LeftArrow,  UnityEngine.InputSystem.Key.A); } }
    public static bool Right { get { return P(UnityEngine.InputSystem.Key.RightArrow, UnityEngine.InputSystem.Key.D); } }
    public static bool Up    { get { return P(UnityEngine.InputSystem.Key.UpArrow,    UnityEngine.InputSystem.Key.W); } }
    public static bool Down  { get { return P(UnityEngine.InputSystem.Key.DownArrow,  UnityEngine.InputSystem.Key.S); } }

    public static bool ConfirmKey
    {
        get
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame
                               || kb.numpadEnterKey.wasPressedThisFrame
                               || kb.spaceKey.wasPressedThisFrame);
        }
    }

    public static bool Click
    {
        get
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }
    }

    public static bool Cancel
    {
        get
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
    }
#else
    static bool P(KeyCode a, KeyCode b) { return Input.GetKeyDown(a) || Input.GetKeyDown(b); }

    public static bool Left { get { return P(KeyCode.LeftArrow, KeyCode.A); } }
    public static bool Right { get { return P(KeyCode.RightArrow, KeyCode.D); } }
    public static bool Up { get { return P(KeyCode.UpArrow, KeyCode.W); } }
    public static bool Down { get { return P(KeyCode.DownArrow, KeyCode.S); } }

    public static bool ConfirmKey
    {
        get { return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space); }
    }

    public static bool Click { get { return Input.GetMouseButtonDown(0); } }
    public static bool Cancel { get { return Input.GetKeyDown(KeyCode.Escape); } }
#endif
}