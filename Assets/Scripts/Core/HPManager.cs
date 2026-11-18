using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 체력(HP) 전역 관리자.
/// - Health(플레이어 본체 컴포넌트)와 HealthUI 사이를 일관되게 연결한다.
/// - 씬 전환/리로드 시 플레이어 Health를 자동으로 재탐색하고 기본 최대 체력(5칸)을 보장한다.
/// - 낙사 판정(ApplyFallHazard) 및 피격/회복/사망 이벤트를 통합 중계한다.
/// </summary>
public class HPManager : MonoBehaviour
{
    private static HPManager instance;
    private static bool isQuitting;

    [Header("플레이어 체력 기본 설정")]
    [SerializeField, Min(1)] private int defaultPlayerMaxHp = 5;
    [SerializeField, Min(0f)] private float defaultHitInvulnerableTime = 0.8f;
    [SerializeField] private bool fullHealOnSceneLoad = true;

    [Header("낙사(Fall Hazard) 설정")]
    [SerializeField, Min(1)] private int fallHazardDamage = 1;

    private Health playerHealth;
    private float rebindTimer;

    public static HPManager Instance
    {
        get
        {
            if (instance == null && !isQuitting)
            {
                instance = FindObjectOfType<HPManager>(true);
                if (instance == null)
                {
                    GameObject go = new GameObject("HPManager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<HPManager>();
                }
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

    public int CurrentHp => PlayerHealth != null ? PlayerHealth.Current : defaultPlayerMaxHp;
    public int MaxHp => PlayerHealth != null ? PlayerHealth.Max : defaultPlayerMaxHp;
    public bool IsPlayerDead => PlayerHealth != null && PlayerHealth.IsDead;

    public event System.Action<Health> PlayerBound;
    public event System.Action<int, int> PlayerHpChanged;
    public event System.Action<DamageInfo> PlayerDamaged;
    public event System.Action<Health> PlayerDied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
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

    private void OnApplicationQuit()
    {
        isQuitting = true;
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

    private void Update()
    {
        if (playerHealth == null)
        {
            rebindTimer -= Time.unscaledDeltaTime;
            if (rebindTimer <= 0f)
            {
                rebindTimer = 0.25f;
                RefreshPlayerBinding();
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        rebindTimer = 0f;
        RefreshPlayerBinding();
    }

    /// <summary>현재 씬의 플레이어 Health를 다시 찾아 연결한다.</summary>
    public void RefreshPlayerBinding()
    {
        BindPlayerHealth(FindPlayerHealth());
    }

    /// <summary>현재 씬에서 플레이어 오브젝트의 Health 컴포넌트를 탐색한다.</summary>
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
            playerHealth.Configure(Team.Player, defaultPlayerMaxHp, defaultHitInvulnerableTime);
        }
        else if (fullHealOnSceneLoad)
        {
            playerHealth.ResetToMax();
        }

        playerHealth.Damaged += OnPlayerDamaged;
        playerHealth.Changed += OnPlayerChanged;
        playerHealth.Died += OnPlayerDied;

        PlayerBound?.Invoke(playerHealth);
        PlayerHpChanged?.Invoke(playerHealth.Current, playerHealth.Max);
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
        PlayerDamaged?.Invoke(info);
    }

    private void OnPlayerChanged(int current, int max)
    {
        PlayerHpChanged?.Invoke(current, max);
    }

    private void OnPlayerDied(Health h)
    {
        PlayerDied?.Invoke(h);
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

    /// <summary>낙사(deathY 이하 추락) 시 데미지를 적용하고 생존 시 리스폰 위치로 복귀시킨다.</summary>
    public void ApplyFallHazard(PlayerRespawn respawn)
    {
        if (respawn == null) return;

        DamagePlayer(fallHazardDamage, Vector2.zero, respawn.gameObject);

        if (!IsPlayerDead)
            respawn.Respawn();
    }

    public void SetPlayerMaxHealth(int newMax, bool refill = true)
    {
        defaultPlayerMaxHp = Mathf.Max(1, newMax);
        if (PlayerHealth != null)
            PlayerHealth.SetMax(defaultPlayerMaxHp, refill);
    }
}