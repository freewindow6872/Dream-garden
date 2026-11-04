using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GOALCREATOR
{
    [MenuItem("Gardener/GOAL 생성")]
    static void CreateGoal()
    {
        GameObject goal = new GameObject("GOAL");
        Undo.RegisterCreatedObjectUndo(goal, "Create GOAL");

        // 스프라이트 (유니티 기본 원형 스프라이트, 노란색)
        SpriteRenderer sr = goal.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sr.color = new Color(1f, 0.85f, 0.2f, 1f);
        sr.sortingOrder = 5;

        // 닿으면 판정되는 트리거 콜라이더
        BoxCollider2D col = goal.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // 클리어 판정 스크립트
        goal.AddComponent<STAGECLEARTRIGGER>();

        goal.transform.localScale = new Vector3(3f, 3f, 1f);

        // 씬 뷰 중앙에 배치
        if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pos = SceneView.lastActiveSceneView.pivot;
            pos.z = 0f;
            goal.transform.position = pos;
        }

        Selection.activeGameObject = goal;
        EditorSceneManager.MarkSceneDirty(goal.scene);
    }
}