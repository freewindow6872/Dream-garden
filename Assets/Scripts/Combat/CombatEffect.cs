using UnityEngine;

/// <summary>
/// 이펙트 1종 설정. 인스펙터에서 프리팹 또는 스프라이트 프레임을 넣으면 된다.
/// 모든 이펙트는 "오른쪽을 보는 기준"으로 제작하고, 왼쪽이면 자동으로 좌우 반전된다.
/// </summary>
[System.Serializable]
public class EffectSpec
{
    [Tooltip("이펙트 프리팹 (있으면 프레임보다 우선). 오른쪽 기준으로 제작")]
    public GameObject prefab;
    [Tooltip("프리팹이 없을 때 재생할 스프라이트 프레임들 (오른쪽 기준)")]
    public Sprite[] frames;
    public float fps = 24f;
    [Tooltip("오른쪽을 볼 때 기준 위치 (왼쪽이면 x 자동 반전)")]
    public Vector2 offset;
    public float scale = 1f;
    public Color color = Color.white;
    [Tooltip("true면 이펙트가 주인(플레이어)을 따라 움직인다")]
    public bool followOwner = false;
    public int sortingOrder = 6;
    [Tooltip("프리팹 이펙트가 자동으로 사라지기까지의 시간(초)")]
    public float prefabLifetime = 1f;

    public bool IsEmpty { get { return prefab == null && (frames == null || frames.Length == 0); } }
}

/// <summary>이펙트 생성 도우미. 모든 이펙트는 unscaled 시간으로 재생되어 히트스톱 중에도 멈추지 않는다.</summary>
public static class CombatEffect
{
    /// <param name="dir">+1 오른쪽, -1 왼쪽 (왼쪽이면 그래픽 좌우 반전)</param>
    public static GameObject Spawn(EffectSpec spec, Vector2 position, int dir, Transform owner)
    {
        if (spec == null || spec.IsEmpty) return null;

        Vector3 pos = new Vector3(position.x + spec.offset.x * dir, position.y + spec.offset.y, 0f);
        GameObject go;

        if (spec.prefab != null)
        {
            go = Object.Instantiate(spec.prefab, pos, Quaternion.identity);
            Vector3 s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x * spec.scale * (dir < 0 ? -1f : 1f), s.y * spec.scale, s.z);
            Object.Destroy(go, Mathf.Max(0.05f, spec.prefabLifetime));
        }
        else
        {
            go = new GameObject("Effect");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(spec.scale, spec.scale, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spec.frames[0];
            sr.flipX = dir < 0;
            sr.color = spec.color;
            sr.sortingOrder = spec.sortingOrder;

            go.AddComponent<SpriteFrameEffect>().Init(sr, spec.frames, spec.fps);
        }

        if (spec.followOwner && owner != null) go.transform.SetParent(owner, true);
        return go;
    }

    /// <summary>에셋이 아직 없을 때 쓰는 임시 이펙트 (작은 사각형이 퍼지며 사라짐)</summary>
    public static void SpawnDebugBurst(Vector2 position, Color color, float size)
    {
        GameObject go = new GameObject("DebugBurst");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.White;
        sr.color = color;
        sr.sortingOrder = 7;
        go.AddComponent<DebugBurst>().size = size;
    }
}

/// <summary>스프라이트 프레임을 순서대로 재생하고 끝나면 스스로 삭제</summary>
public class SpriteFrameEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite[] frames;
    private float fps = 24f;
    private float time;

    public void Init(SpriteRenderer renderer, Sprite[] sprites, float framesPerSecond)
    {
        sr = renderer;
        frames = sprites;
        fps = Mathf.Max(1f, framesPerSecond);
    }

    private void Update()
    {
        if (sr == null || frames == null) { Destroy(gameObject); return; }

        time += Time.unscaledDeltaTime;
        int index = Mathf.FloorToInt(time * fps);
        if (index >= frames.Length) { Destroy(gameObject); return; }
        sr.sprite = frames[index];
    }
}

public class DebugBurst : MonoBehaviour
{
    public float size = 0.6f;
    private const float Duration = 0.15f;
    private float t;
    private SpriteRenderer sr;

    private void Awake() { sr = GetComponent<SpriteRenderer>(); }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(t / Duration);
        float s = Mathf.Lerp(size * 0.5f, size, k);
        transform.localScale = new Vector3(s, s, 1f);

        Color c = sr.color;
        c.a = 1f - k;
        sr.color = c;

        if (k >= 1f) Destroy(gameObject);
    }
}
