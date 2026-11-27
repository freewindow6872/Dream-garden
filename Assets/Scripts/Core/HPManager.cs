using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 체력(HP) 전역 관리자.
/// - Health(플레이어 본체 컴포넌트)와 HealthUI 사이를 일관되게 연결한다.
/// - 씬 전환/리로드 시 플레이어 Health를 자동으로 재탐색하고 기본 최대 체력(5칸)을 보장한다.
/// - 피격/회복/사망 이벤트를 중계하여 다른 시스템이 플레이어 오브젝트를 직접 찾지 않아도 구독할 수 있게 한다.
/// </summary>
public class HPManager : MonoBehaviour
{
    private static HPManager instance;

    [Header("플레이어 체력 기본 설정")]
    [SerializeField, Min(1)] private int defaultPlayerMaxHp = 5;
    [SerializeField] private bool fullHealOnSceneLoad = true;

    private Health playerHealth;

    public static HPManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("HPManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HPManager>();
            }
            return instance;
        }
    }

    public Health PlayerHealth
    {
        get
        {
            if (playerHealth == null)
                BindPlayerHealth(FindPlayerHealth());
            return playerHealth;
        }
    }

    public int CurrentHp { get { return PlayerHealth != null ? PlayerHealth.Current : defaultPlayerMaxHp; } }
    public int MaxHp { get { return PlayerHealth != null ? PlayerHealth.Max : defaultPlayerMaxHp; } }
    public bool IsPlayerDead { get { return PlayerHealth != null && PlayerHealth.IsDead; } }

    public event System.Action<Health> PlayerBound;
    public event System.Action<int, int> PlayerHpChanged;
    public event System.Action<DamageInfo> PlayerDamaged;
    public event System.Action<Health> PlayerDied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindPlayerHealth();
            instance = null;
        }
    }

    private void Start()
    {
        RefreshPlayerBinding();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshPlayerBinding();
    }

    /// <summary>현재 씬의 플레이어 Health를 다시 찾아 연결한다.</summary>
    public void RefreshPlayerBinding()
    {
        BindPlayerHealth(FindPlayerHealth());
    }

    public Health FindPlayerHealth()
    {
        PlayerCombat combat = FindObjectOfType<PlayerCombat>();
        if (combat != null)
        {
            Health h = combat.GetComponent<Health>();
            if (h != null) return h;
        }

        Health[] all = FindObjectsOfType<Health>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].Team == Team.Player)
                return all[i];
        }

        return null;
    }

    private void BindPlayerHealth(Health target)
    {
        if (target == null) return;
        if (playerHealth == target) return;

        UnbindPlayerHealth();
        playerHealth = target;

        if (playerHealth.Team != Team.Player || playerHealth.Max != defaultPlayerMaxHp)
        {
            playerHealth.Configure(Team.Player, defaultPlayerMaxHp, 0.8f);
        }
        else if (fullHealOnSceneLoad)
        {
            playerHealth.ResetToMax();
        }

        if (PlayerBound != null) PlayerBound(playerHealth);

        playerHealth.Damaged += OnPlayerDamaged;
        playerHealth.Changed += OnPlayerChanged;
        playerHealth.Died += OnPlayerDied;

        if (PlayerHpChanged != null)
            PlayerHpChanged(playerHealth.Current, playerHealth.Max);
    }

    private void UnbindPlayerHealth()
    {
        if (playerHealth == null) return;

        playerHealth.Damaged -= OnPlayerDamaged;
        playerHealth.Changed -= OnPlayerChanged;
        playerHealth.Died -= OnPlayerDied;
        playerHealth = null;
    }

    private void OnPlayerDamaged(DamageInfo info)
    {
        if (PlayerDamaged != null) PlayerDamaged(info);
    }

    private void OnPlayerChanged(int current, int max)
    {
        if (PlayerHpChanged != null) PlayerHpChanged(current, max);
    }

    private void OnPlayerDied(Health h)
    {
        if (PlayerDied != null) PlayerDied(h);
    }

    // ---------------------------------------------------------------- public API

    public void HealPlayer(int amount)
    {
        if (PlayerHealth == null || amount <= 0) return;
        PlayerHealth.Heal(amount);
    }

    public void FullHealPlayer()
    {
        if (PlayerHealth == null) return;
        PlayerHealth.ResetToMax();
    }

    public bool DamagePlayer(int amount, Vector2 knockback = default(Vector2), GameObject source = null)
    {
        if (PlayerHealth == null || amount <= 0) return false;

        DamageInfo info = new DamageInfo
        {
            amount = amount,
            knockback = knockback,
            sourceTeam = Team.Neutral,
            source = source,
            hitPoint = PlayerHealth.transform.position
        };
        return PlayerHealth.TakeDamage(info);
    }


    /// <summary>낙하 위험 구역 처리: 플레이어에게 피해를 주고 지정된 리스폰 위치로 이동시킨다.</summary>
    public void ApplyFallHazard(PlayerRespawn respawn)
    {
        if (respawn == null || PlayerHealth == null || PlayerHealth.IsDead) return;
        DamagePlayer(1, Vector2.zero, respawn.gameObject);
        if (!PlayerHealth.IsDead) respawn.Respawn();
    }

    public void SetPlayerMaxHealth(int newMax)
    {
        defaultPlayerMaxHp = Mathf.Max(1, newMax);
        if (PlayerHealth != null)
            PlayerHealth.Configure(Team.Player, defaultPlayerMaxHp, 0.8f);
    }
}