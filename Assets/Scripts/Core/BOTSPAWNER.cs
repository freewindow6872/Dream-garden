using UnityEngine;

public class BOTSPAWNER : MonoBehaviour
{
    const string RESOURCEPATH = "BOTLAYOUTS";

    [Tooltip("체크하면 씬이 시작될 때 자동으로 배치")]
    public bool spawnOnStart = true;

    [Header("테스트용 (0이면 GAMEDATA의 선택값 사용)")]
    public int testStage = 0;
    public int testSubStage = 0;

    // Awake에서 생성해야 다른 스크립트의 Start보다 먼저 적이 존재함
    void Awake()
    {
        if (!spawnOnStart) return;

        int stage = testStage > 0 ? testStage : GAMEDATA.Stage;
        int sub = testSubStage > 0 ? testSubStage : GAMEDATA.SubStage;
        Spawn(stage, sub);
    }

    public void Spawn(int stage, int sub)
    {
        BOTLAYOUT layout = null;
        foreach (BOTLAYOUT l in Resources.LoadAll<BOTLAYOUT>(RESOURCEPATH))
        {
            if (l.stage == stage && l.subStage == sub) { layout = l; break; }
        }

        if (layout == null)
        {
            Debug.Log("[봇] " + stage + "-" + sub + " 배치 데이터가 없어 생성하지 않습니다.");
            return;
        }

        GameObject root = new GameObject("BOTS");
        foreach (BOTLAYOUT.ENTRY e in layout.entries)
        {
            if (e.prefab == null) continue;

            GameObject go = Instantiate(e.prefab, e.position, Quaternion.Euler(0f, 0f, e.rotationZ), root.transform);
            if (e.scale != Vector3.zero) go.transform.localScale = e.scale;
        }

        Debug.Log("[봇] " + stage + "-" + sub + " 배치: " + layout.entries.Count + "마리 생성");
    }
}