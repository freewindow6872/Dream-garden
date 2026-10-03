using System.Collections.Generic;

/// <summary>
/// 플레이어 진행도. (저장/불러오기는 TODO)
/// 대시/이단점프는 기획서 2.2에 따라 "강화 시스템으로 획득" 하는 값.
/// </summary>
[System.Serializable]
public class PlayerProgress
{
    public bool dashUnlocked = false;
    public bool doubleJumpUnlocked = false;
    public string currentWeaponId = "old_garden_scissors";
    public HashSet<string> clearedStages = new HashSet<string>();

    public bool IsCleared(string stageId) { return clearedStages.Contains(stageId); }

    /// <summary>처음 클리어면 true (첫 클리어 보상 지급 판단용)</summary>
    public bool MarkCleared(string stageId)
    {
        return !string.IsNullOrEmpty(stageId) && clearedStages.Add(stageId);
    }
}
