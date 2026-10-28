using UnityEngine;

/// <summary>씬과 독립적인 골드 카운터 UI. 봇이 있는 씬에서 자동으로 하나만 유지한다.</summary>
public class GoldCounter : MonoBehaviour
{
    public static int Gold { get; private set; }
    private static GoldCounter instance;
    private GUIStyle style;
    [SerializeField] private string label = "Gold";
    [SerializeField] private Vector2 screenOffset = new Vector2(20f, 20f);
    [SerializeField, Min(10)] private int fontSize = 24;

    public static GoldCounter EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindObjectOfType<GoldCounter>();
        if (instance == null) instance = new GameObject("Gold Counter UI").AddComponent<GoldCounter>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void Add(int amount) { Gold += Mathf.Max(0, amount); }

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.UpperRight;
        }
        GUI.Label(new Rect(Screen.width - 190f - screenOffset.x, screenOffset.y, 170f, 40f), label + ": " + Gold, style);
    }
}
