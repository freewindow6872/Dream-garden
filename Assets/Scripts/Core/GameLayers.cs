using UnityEngine;

/// <summary>레이어 조회 헬퍼. "Ground" 레이어 이름이 없으면 기존 씬이 쓰던 3번 레이어로 대체.</summary>
public static class GameLayers
{
    public const string GroundName = "Ground";
    public const int GroundFallbackIndex = 3;

    public static int Ground
    {
        get
        {
            int layer = LayerMask.NameToLayer(GroundName);
            return layer >= 0 ? layer : GroundFallbackIndex;
        }
    }

    public static int GroundMaskValue { get { return 1 << Ground; } }
}
