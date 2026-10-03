using System.Collections;
using UnityEngine;

/// <summary>
/// 타격감용 히트스톱. (기획서 1.6: 타격감 우선)
/// 주의: Time.timeScale을 직접 만지므로, 나중에 일시정지 기능을 넣으면 이 파일도 같이 수정할 것.
/// </summary>
public class HitStop : MonoBehaviour
{
    private static HitStop instance;
    private Coroutine routine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    public static void Do(float seconds)
    {
        if (seconds <= 0f) return;

        if (instance == null)
        {
            GameObject go = new GameObject("HitStop");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitStop>();
        }
        instance.Begin(seconds);
    }

    private void Begin(float seconds)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(seconds));
    }

    private IEnumerator Run(float seconds)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = 1f;
        routine = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Time.timeScale = 1f;
        }
    }
}
