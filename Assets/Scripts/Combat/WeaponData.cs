using UnityEngine;

/// <summary>콤보 1타의 데이터. 시간 단위는 초.</summary>
[System.Serializable]
public class ComboStep
{
    public float startup = 0.05f;
    public float active = 0.09f;
    public float recovery = 0.12f;
    [Tooltip("이 타격 이후 다음 타를 이어갈 수 있는 유예 시간. 0이면 콤보 종료")]
    public float comboWindow = 0.3f;
    public float damageMultiplier = 1f;
    public Vector2 hitboxSize = new Vector2(1.5f, 1.1f);
    [Tooltip("플레이어가 오른쪽을 볼 때 기준 (왼쪽이면 x가 자동 반전)")]
    public Vector2 hitboxOffset = new Vector2(1.0f, 0f);
    public Vector2 knockback = new Vector2(4f, 1.5f);
    public float lunge = 3f;
}

/// <summary>
/// 기본 무기 데이터. (기획서 3.4.3 표: 낡은 정원 가위 4/6, 정원 가위 7/10)
/// ※ "4 / 6" 을 (최소 / 최대) 데미지로 해석함. 다르면 기획 확인 필요.
/// </summary>
[CreateAssetMenu(menuName = "Gardener/Weapon Data", fileName = "Weapon_New")]
public class WeaponData : ScriptableObject
{
    public string id = "old_garden_scissors";
    public string displayName = "낡은 정원 가위";
    public int damageMin = 4;
    public int damageMax = 6;
    public ComboStep[] combo = new ComboStep[0];

    public int RollDamage(float multiplier)
    {
        int roll = Random.Range(damageMin, damageMax + 1);
        return Mathf.Max(1, Mathf.RoundToInt(roll * multiplier));
    }

#if UNITY_EDITOR
    [ContextMenu("기본값 채우기 (낡은 정원 가위 3타 콤보)")]
    private void FillDefaultsFromMenu()
    {
        ApplyOldScissorsDefaults();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    /// <summary>낡은 정원 가위: 3타 콤보 (마지막 타는 느리지만 강하고 넉백이 큼)</summary>
    public void ApplyOldScissorsDefaults()
    {
        id = "old_garden_scissors";
        displayName = "낡은 정원 가위";
        damageMin = 4;
        damageMax = 6;
        combo = new ComboStep[]
        {
            new ComboStep { startup = 0.05f, active = 0.09f, recovery = 0.12f, comboWindow = 0.30f, damageMultiplier = 1.0f,
                hitboxSize = new Vector2(1.5f, 1.1f), hitboxOffset = new Vector2(1.0f, 0f), knockback = new Vector2(4f, 1.5f), lunge = 3f },
            new ComboStep { startup = 0.05f, active = 0.09f, recovery = 0.12f, comboWindow = 0.30f, damageMultiplier = 1.0f,
                hitboxSize = new Vector2(1.5f, 1.1f), hitboxOffset = new Vector2(1.0f, 0f), knockback = new Vector2(4.5f, 1.5f), lunge = 3f },
            new ComboStep { startup = 0.09f, active = 0.12f, recovery = 0.28f, comboWindow = 0f, damageMultiplier = 1.6f,
                hitboxSize = new Vector2(1.9f, 1.3f), hitboxOffset = new Vector2(1.2f, 0.1f), knockback = new Vector2(8f, 4f), lunge = 5f },
        };
    }

    public static WeaponData CreateRuntimeDefault()
    {
        WeaponData w = CreateInstance<WeaponData>();
        w.ApplyOldScissorsDefaults();
        return w;
    }
}
