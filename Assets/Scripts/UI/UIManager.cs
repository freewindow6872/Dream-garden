using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬별 HUD 표시 규칙, 팝업(인벤토리/화분/장비/결과창) 열림 상태, 게임 일시정지를 통합 관리하는 UI 매니저.
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager instance;
    private static bool isQuitting;

    public static bool HasInstance => instance != null;

    public static UIManager Instance
    {
        get
        {
            if (instance == null && !isQuitting)
            {
                instance = FindObjectOfType<UIManager>(true);
                if (instance == null)
                {
                    GameObject go = new GameObject("UIManager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<UIManager>();
                }
            }
            return instance;
        }
    }

    [Header("씬별 UI 표시 규칙")]
    [Tooltip("체력 HUD를 숨길 씬 이름 목록")]
    [SerializeField] private List<string> hudHiddenScenes = new List<string> { "title", "GAMETITLE", "STAGESELECT" };

    [Tooltip("인벤토리(I키) 열기를 차단할 씬 이름 목록")]
    [SerializeField] private List<string> inventoryBlockedScenes = new List<string> { "title", "GAMETITLE" };

    [Header("팝업 연동")]
    [Tooltip("팝업이 열려 있을 때 플레이어 이동/공격 입력을 자동으로 잠글지 여부")]
    [SerializeField] private bool lockPlayerInputWhenPopupOpen = true;

    private readonly HashSet<string> openPopups = new HashSet<string>();
    private bool isPaused;

    public bool IsPaused => isPaused;
    public bool IsAnyPopupOpen => openPopups.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var _ = Instance;
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
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        openPopups.Clear();
        SetPaused(false);
        ApplySceneUIRules(scene.name);
    }

    public bool IsHudAllowed(string sceneName)
    {
        return !hudHiddenScenes.Contains(sceneName);
    }

    public bool IsInventoryAllowed(string sceneName)
    {
        return !inventoryBlockedScenes.Contains(sceneName);
    }

    public void ApplySceneUIRules(string sceneName)
    {
        HealthUI[] healthUIs = FindObjectsOfType<HealthUI>(true);
        bool showHud = IsHudAllowed(sceneName);
        for (int i = 0; i < healthUIs.Length; i++)
            healthUIs[i].SetHudVisible(showHud);

        if (INVENTORYUI.Instance != null)
            INVENTORYUI.Instance.SetAllowed(IsInventoryAllowed(sceneName));
    }

    public void SetPopupOpen(string popupId, bool isOpen, bool pauseGame = false)
    {
        if (string.IsNullOrEmpty(popupId)) return;

        if (isOpen)
            openPopups.Add(popupId);
        else
            openPopups.Remove(popupId);

        if (pauseGame)
            SetPaused(isOpen);

        if (lockPlayerInputWhenPopupOpen)
        {
            PlayerController2D player = FindObjectOfType<PlayerController2D>();
            if (player != null && HPManager.Instance != null && !HPManager.Instance.IsPlayerDead)
                player.SetInputLocked(IsAnyPopupOpen);
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    public void ToggleInventory()
    {
        if (INVENTORYUI.Instance != null)
            INVENTORYUI.Instance.Toggle();
    }

    public void ShowStageResult()
    {
        RESULTSCREEN result = FindObjectOfType<RESULTSCREEN>(true);
        if (result == null)
        {
            Debug.LogWarning("[UIManager] 현재 씬에 RESULTSCREEN이 없습니다.");
            return;
        }
        SetPopupOpen("ResultScreen", true, true);
        result.Show();
    }

    public void LoadScene(string sceneName)
    {
        openPopups.Clear();
        SetPaused(false);
        SceneManager.LoadScene(sceneName);
    }
}