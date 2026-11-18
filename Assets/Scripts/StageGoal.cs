using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵 에디터로 배치한 Stage/Markers/Goal 에 플레이어가 닿으면 스테이지 클리어 처리.
/// RESULTSCREEN이 씬에 있으면 UIManager를 통해 결과창을 띄우고, 없으면 기본 클리어 UI를 표시한다.
/// </summary>
public class StageGoal : MonoBehaviour
{
    [SerializeField] private Vector2 goalSize = new Vector2(1f, 1f);
    [SerializeField] private KeyCode quickRestartKey = KeyCode.R;

    private PlayerController2D player;
    private Collider2D playerCol;
    private bool cleared;
    private bool hasResultScreen;

    public bool IsCleared => cleared;
    public event System.Action Cleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        GameObject go = GameObject.Find("Goal");
        if (go == null) return;
        if (go.transform.parent == null || go.transform.parent.name != "Markers") return;
        if (go.GetComponent<StageGoal>() == null) go.AddComponent<StageGoal>();
    }

    private void Start()
    {
        player = FindObjectOfType<PlayerController2D>();
        if (player != null) playerCol = player.GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (cleared)
        {
            if (!hasResultScreen && Input.GetKeyDown(quickRestartKey))
                UIManager.Instance.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (playerCol == null) return;

        Bounds goal = new Bounds(transform.position, new Vector3(goalSize.x, goalSize.y, 10f));
        if (goal.Intersects(playerCol.bounds))
            Clear();
    }

    private void Clear()
    {
        cleared = true;
        if (player != null) player.SetInputLocked(true);
        GameManager.Instance.Progress.MarkCleared(SceneManager.GetActiveScene().name);
        Cleared?.Invoke();

        RESULTSCREEN result = FindObjectOfType<RESULTSCREEN>(true);
        hasResultScreen = result != null;
        if (hasResultScreen)
            UIManager.Instance.ShowStageResult();
    }

    private void OnGUI()
    {
        if (!cleared || hasResultScreen) return;

        GUIStyle big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 48,
            fontStyle = FontStyle.Bold
        };
        big.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 80), "STAGE CLEAR", big);

        big.fontSize = 22;
        GUI.Label(new Rect(0, Screen.height * 0.35f + 80, Screen.width, 40), quickRestartKey + " : 다시 시작", big);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
        Gizmos.DrawWireCube(transform.position, goalSize);
    }
}