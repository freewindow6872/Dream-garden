using UnityEngine;

/// <summary>
/// 입력 창구. 모든 스크립트는 Input을 직접 부르지 말고 여기를 통해서 읽는다.
/// (나중에 New Input System으로 바꿀 때 이 파일만 수정하면 된다)
///
/// 키 배치 (이 외의 키/마우스/패드는 전부 사용하지 않음):
///   이동  : ← → 방향키
///   점프  : C
///   공격  : X
///   대시  : Z
/// Input Manager의 "Horizontal"/"Jump" 축은 WASD·Space·조이스틱까지 같이 읽기 때문에 쓰지 않고,
/// KeyCode를 직접 읽어서 위 키만 동작하게 한다.
/// </summary>
public static class GameInput
{
    public static KeyCode LeftKey = KeyCode.LeftArrow;
    public static KeyCode RightKey = KeyCode.RightArrow;
    public static KeyCode JumpKey = KeyCode.C;
    public static KeyCode AttackKey = KeyCode.X;
    public static KeyCode DashKey = KeyCode.Z;

    public static float Horizontal
    {
        get
        {
            float h = 0f;
            if (Input.GetKey(LeftKey)) h -= 1f;
            if (Input.GetKey(RightKey)) h += 1f;
            return h;
        }
    }

    public static bool JumpDown { get { return Input.GetKeyDown(JumpKey); } }
    public static bool JumpUp { get { return Input.GetKeyUp(JumpKey); } }
    public static bool JumpHeld { get { return Input.GetKey(JumpKey); } }
    public static bool DashDown { get { return Input.GetKeyDown(DashKey); } }
    public static bool AttackDown { get { return Input.GetKeyDown(AttackKey); } }
}
