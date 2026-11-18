using System.Collections;
using UnityEngine;

/// <summary>
/// 타격감용 히트스톱. UIManager가 일시정지 중일 때는 timeScale을 덮어쓰지 않는다.
/// </summary>
public class HitStop : MonoBehaviour
{
    private static HitStop instance;
    private static bool isQuitting;

    [Header("히트스톱 설정")]
    [SerializeField, Range(0.01f, 0.5f)] private float frozenTimeScale = 0.05f;
    [SerializeField, Min(0.01f)] private float defaultDuration = 0.06f;

    private Coroutine routine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    public static void Do(float seconds)
    {
        if (seconds <= 0f || isQuitting) return;

        if (instance == null)
        {
            instance = FindObjectOfType<HitStop>(true);
            if (instance == null)
            {
                GameObject go = new GameObject("HitStop");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HitStop>();
            }
        }
        instance.Begin(seconds);
    }

    public static void DoDefault()
    {
        float dur = instance != null ? instance.defaultDuration : 0.06f;
        Do(dur);
    }

    private void Begin(float seconds)
    {
        if (UIManager.HasInstance && UIManager.Instance.IsPaused) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(seconds));
    }

    private IEnumerator Run(float seconds)
    {
        Time.timeScale = frozenTimeScale;
        yield return new WaitForSecondsRealtime(seconds);

        if (!UIManager.HasInstance || !UIManager.Instance.IsPaused)
            Time.timeScale = 1f;

        routine = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            if (!isQuitting && (!UIManager.HasInstance || !UIManager.Instance.IsPaused))
                Time.timeScale = 1f;
        }
    }
}