using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenerEditor
{
    /// <summary>
    /// 시작점/골 위치를 Scene 뷰에 항상 표시한다. (맵 에디터 창이 닫혀 있어도 보임)
    /// 씬 데이터는 건드리지 않고 화면에만 그린다. 에디터 전용.
    /// </summary>
    [InitializeOnLoad]
    public static class MapEditorGizmos
    {
        private static GUIStyle labelStyle;

        static MapEditorGizmos()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint) return;

            Transform start = Find(MapNames.PlayerStart);
            Transform goal = Find(MapNames.Goal);
            if (start == null && goal == null) return;

            CompareFunction prevZ = Handles.zTest;
            Handles.zTest = CompareFunction.Always;

            if (start != null) DrawMarker(start.position, "START", new Color(0.3f, 1f, 0.4f, 1f));
            if (goal != null) DrawMarker(goal.position, "GOAL", new Color(1f, 0.4f, 0.3f, 1f));

            Handles.zTest = prevZ;
        }

        private static Transform Find(string markerName)
        {
            GameObject go = GameObject.Find(MapNames.Stage + "/" + MapNames.Markers + "/" + markerName);
            return go != null ? go.transform : null;
        }

        private static void DrawMarker(Vector3 pos, string text, Color color)
        {
            Handles.color = color;
            // 플레이어 몸(1x1) 크기 박스
            Handles.DrawWireCube(new Vector3(pos.x, pos.y, 0f), new Vector3(1f, 1f, 0f));

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.boldLabel);
                labelStyle.alignment = TextAnchor.MiddleCenter;
            }
            labelStyle.normal.textColor = color;
            Handles.Label(new Vector3(pos.x - 0.35f, pos.y + 0.95f, 0f), text, labelStyle);
        }
    }
}
