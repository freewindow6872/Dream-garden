using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵 에디터로 배치한 Stage/Markers/Goal 에 플레이어가 닿으면 스테이지 클리어.
/// 씬에 따로 붙일 필요 없음: 플레이 시작 시 Goal 오브젝트를 찾아 자동으로 붙는다.
/// </summary>
public class StageGoal : MonoBehaviour
{
    [SerializeField] private Vector2 goalSize = new Vector2(1f, 1f);

    private PlayerController2D player;
    private Collider2D playerCol;
    private bool cleared;

    public bool IsCleared { get { return cleared; } }
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
            if (Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
        player.SetInputLocked(true);
        GameManager.Instance.Progress.MarkCleared(SceneManager.GetActiveScene().name);
        if (Cleared != null) Cleared();
    }

    private void OnGUI()
    {
        if (!cleared) return;

        GUIStyle big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 48,
            fontStyle = FontStyle.Bold
        };
        big.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 80), "STAGE CLEAR", big);

        big.fontSize = 22;
        GUI.Label(new Rect(0, Screen.height * 0.35f + 80, Screen.width, 40), "R : 다시 시작", big);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
        Gizmos.DrawWireCube(transform.position, goalSize);
    }
}
