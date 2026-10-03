using UnityEngine;

public enum Team { Player, Enemy, Neutral }

/// <summary>공격 한 번의 정보. Health.TakeDamage에 넘긴다.</summary>
public struct DamageInfo
{
    public int amount;
    public Vector2 knockback;
    public Team sourceTeam;
    public GameObject source;
    public bool isCritical;
}
