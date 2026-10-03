using UnityEngine;

/// <summary>기획서 2.1 플레이 구조의 큰 흐름.</summary>
public enum GameFlow { Garden, StagePrep, Stage, Result }

/// <summary>
/// 전역 상태 보관소. 씬 전환 후에도 유지된다.
/// TODO: 정원 씬 / 스테이지 씬 전환 (LoadGarden, EnterStage), 저장/불러오기
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("GameManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameManager>();
            }
            return instance;
        }
    }

    public GameFlow Flow { get; set; }
    public PlayerProgress Progress { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        Flow = GameFlow.Stage;
        if (Progress == null) Progress = new PlayerProgress();
    }
}
