using UnityEngine;

/// <summary>
/// 데미지 숫자 팝업. 위로 떠오르며 서서히 사라진다. (월드 좌표 TextMesh, 에셋 불필요)
/// 시간은 unscaled 기준이라 히트스톱 중에도 같은 속도로 움직인다.
///
/// 코드에서 직접 띄울 때:  DamagePopup.Spawn(position, 5, DamagePopup.NormalColor);
/// 보통은 DamagePopupEmitter를 대상(적/허수아비)에 붙여두면 맞을 때마다 자동으로 나온다.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    public static readonly Color NormalColor = new Color(1f, 0.95f, 0.8f, 1f);
    public static readonly Color CriticalColor = new Color(1f, 0.85f, 0.2f, 1f);
    public static readonly Color PlayerHurtColor = new Color(1f, 0.3f, 0.3f, 1f);

    private const float Lifetime = 0.8f;
    private const float RiseSpeed = 1.8f;
    private const int SortingOrder = 30;

    private static Font cachedFont;

    private TextMesh main;
    private TextMesh outline;
    private Color baseColor;
    private Vector3 velocity;
    private float age;
    private float startScale;

    private static Font GetFont()
    {
        if (cachedFont == null)
        {
            // 2022.3 이상은 LegacyRuntime, 이전 버전은 Arial
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return cachedFont;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cachedFont = null; }

    public static DamagePopup Spawn(Vector2 position, string text, Color color, float scale = 1f)
    {
        GameObject go = new GameObject("DamagePopup");
        // 같은 위치에서 여러 번 맞아도 숫자가 겹치지 않도록 살짝 흩뿌림
        go.transform.position = new Vector3(position.x + Random.Range(-0.25f, 0.25f), position.y + Random.Range(0f, 0.2f), 0f);

        DamagePopup popup = go.AddComponent<DamagePopup>();
        popup.Init(text, color, scale);
        return popup;
    }

    public static DamagePopup Spawn(Vector2 position, int amount, Color color, float scale = 1f)
    {
        return Spawn(position, amount.ToString(), color, scale);
    }

    private void Init(string text, Color color, float scale)
    {
        baseColor = color;
        startScale = scale;
        velocity = new Vector3(Random.Range(-0.4f, 0.4f), RiseSpeed, 0f);

        // 검은 외곽선 역할의 뒷면 글자 (살짝 오프셋)
        outline = CreateText("Outline", text, new Color(0f, 0f, 0f, 1f), SortingOrder - 1, new Vector3(0.03f, -0.03f, 0f));
        main = CreateText("Text", text, color, SortingOrder, Vector3.zero);

        transform.localScale = Vector3.one * startScale * 0.6f;   // 처음에 작게 시작 → 톡 튀어나오는 느낌
    }

    private TextMesh CreateText(string name, string text, Color color, int order, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;

        TextMesh tm = go.AddComponent<TextMesh>();
        tm.font = GetFont();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.fontSize = 64;            // 해상도용. 실제 크기는 characterSize로 조절
        tm.characterSize = 0.085f;
        tm.color = color;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (tm.font != null) mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = order;
        return tm;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(age / Lifetime);

        // 위로 떠오르며 감속
        transform.position += velocity * Time.unscaledDeltaTime;
        velocity.y = Mathf.Lerp(RiseSpeed, 0.2f, k);
        velocity.x *= 1f - Mathf.Min(1f, 3f * Time.unscaledDeltaTime);

        // 처음 0.12초 동안 크기 팝(0.6 → 1.25 → 1.0)
        float pop = age < 0.12f ? Mathf.Lerp(0.6f, 1.25f, age / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((age - 0.12f) / 0.1f));
        transform.localScale = Vector3.one * startScale * pop;

        // 후반 40%에서 투명해짐
        float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
        Color c = baseColor; c.a = alpha;
        main.color = c;
        outline.color = new Color(0f, 0f, 0f, alpha);

        if (k >= 1f) Destroy(gameObject);
    }
}
