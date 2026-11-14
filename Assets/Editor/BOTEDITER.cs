using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BOTEDITOR : EditorWindow
{
    const string PREVIEWROOT = "BOTPREVIEW";
    const string PREFKEY = "BOTEDITOR_PREFABS";
    const string LAYOUTFOLDER = "Assets/Resources/BOTLAYOUTS";

    readonly List<GameObject> prefabs = new List<GameObject>();

    [SerializeField] int selected = -1;
    [SerializeField] int stage = 1;
    [SerializeField] int subStage = 1;
    [SerializeField] int loadedStage = 1;
    [SerializeField] int loadedSub = 1;
    [SerializeField] bool snap = true;
    [SerializeField] float snapSize = 0.5f;
    [SerializeField] float eraseRadius = 0.6f;
    [SerializeField] BOTLAYOUT currentLayout;
    [SerializeField] string lastSnapshot = "";

    bool placing;
    bool erasing;
    bool loaded;
    Vector2 scroll;

    [MenuItem("Tools/봇 에디터")]
    static void OpenWindow()
    {
        GetWindow<BOTEDITOR>("봇 에디터");
    }

    // ---------- 수명 주기 ----------
    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        LoadPrefabList();
        loaded = FindPreviewRoot() != null;     // 스크립트 재컴파일 후 상태 복구
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    // 창을 닫을 때 저장하지 않은 배치가 있으면 확인
    void OnDestroy()
    {
        GameObject root = FindPreviewRoot();
        if (root == null) return;

        if (Snapshot(root) != lastSnapshot &&
            EditorUtility.DisplayDialog("봇 에디터", "저장하지 않은 봇 배치가 있습니다. 저장할까요?", "저장", "버리기"))
            SaveLayout();

        DestroyImmediate(root);
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        GameObject root = FindPreviewRoot();
        if (root == null) return;

        if (Snapshot(root) != lastSnapshot)
        {
            int choice = EditorUtility.DisplayDialogComplex("봇 에디터",
                "저장하지 않은 봇 배치가 있습니다.\nPlay를 시작하면 미리보기가 사라집니다.",
                "저장하고 Play", "저장 안 하고 Play", "Play 취소");

            if (choice == 2) { EditorApplication.isPlaying = false; return; }
            if (choice == 0) SaveLayout();
        }

        DestroyImmediate(root);
        loaded = false;
    }

    // ---------- 프리팹 목록 ----------
    void LoadPrefabList()
    {
        prefabs.Clear();
        string saved = EditorPrefs.GetString(PREFKEY, "");
        foreach (string guid in saved.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null) prefabs.Add(go);
        }
        if (selected >= prefabs.Count) selected = prefabs.Count - 1;
    }

    void SavePrefabList()
    {
        var guids = new List<string>();
        foreach (GameObject p in prefabs)
            guids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p)));
        EditorPrefs.SetString(PREFKEY, string.Join(",", guids));
    }

    // ---------- 창 UI ----------
    void OnGUI()
    {
        EditorGUILayout.LabelField("1. 스테이지 불러오기", EditorStyles.boldLabel);
        stage = Mathf.Max(1, EditorGUILayout.IntField("스테이지", stage));
        subStage = Mathf.Max(1, EditorGUILayout.IntField("세부 스테이지", subStage));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(loaded ? "다시 불러오기" : "불러오기 / 새로 만들기", GUILayout.Height(26)))
            LoadStage();
        using (new EditorGUI.DisabledScope(!loaded))
        {
            if (GUILayout.Button("저장", GUILayout.Height(26))) SaveLayout();
            if (GUILayout.Button("미리보기 지우기", GUILayout.Height(26))) ConfirmClear();
        }
        EditorGUILayout.EndHorizontal();

        if (loaded)
        {
            GameObject root = FindPreviewRoot();
            bool dirty = root != null && Snapshot(root) != lastSnapshot;
            EditorGUILayout.HelpBox(
                "편집 중: 스테이지 " + loadedStage + "-" + loadedSub +
                (currentLayout != null ? "  (저장된 배치 있음)" : "  (새 배치)") +
                "\n봇 수: " + (root != null ? root.transform.childCount : 0) +
                (dirty ? "\n저장하지 않은 변경이 있습니다." : ""),
                dirty ? MessageType.Warning : MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("스테이지 번호를 정하고 [불러오기]를 누르세요.", MessageType.None);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. 적 프리팹 목록", EditorStyles.boldLabel);
        DrawDropArea();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(150));
        int removeAt = -1;
        for (int i = 0; i < prefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            bool isSel = selected == i;
            bool now = GUILayout.Toggle(isSel, prefabs[i].name, "Button");
            if (now && !isSel) selected = i;
            if (GUILayout.Button("X", GUILayout.Width(24))) removeAt = i;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (removeAt >= 0)
        {
            prefabs.RemoveAt(removeAt);
            if (selected >= prefabs.Count) selected = prefabs.Count - 1;
            SavePrefabList();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. 배치", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!loaded))
        {
            bool newPlacing = GUILayout.Toggle(placing, "배치 모드 (Scene 뷰 클릭 = 생성)", "Button", GUILayout.Height(28));
            bool newErasing = GUILayout.Toggle(erasing, "지우기 모드 (클릭한 봇 삭제)", "Button", GUILayout.Height(28));
            if (newPlacing != placing) { placing = newPlacing; if (placing) erasing = false; SceneView.RepaintAll(); }
            if (newErasing != erasing) { erasing = newErasing; if (erasing) placing = false; SceneView.RepaintAll(); }
        }

        snap = EditorGUILayout.Toggle("격자에 맞춤", snap);
        using (new EditorGUI.DisabledScope(!snap))
            snapSize = Mathf.Max(0.05f, EditorGUILayout.FloatField("격자 크기", snapSize));
        eraseRadius = Mathf.Max(0.1f, EditorGUILayout.FloatField("지우기 반경", eraseRadius));

        EditorGUILayout.HelpBox(
            "Alt+드래그: 화면 이동 / Esc: 모드 끄기\n" +
            "배치한 봇은 이동(W)·회전(E)·크기(R) 도구로 자유롭게 조절할 수 있고, 저장하면 함께 기록됩니다.\n" +
            "미리보기는 씬 파일에 저장되지 않습니다. 배치는 [저장]을 눌러야 데이터 파일로 남습니다.",
            MessageType.None);
    }

    void DrawDropArea()
    {
        Rect r = GUILayoutUtility.GetRect(0, 46, GUILayout.ExpandWidth(true));
        GUI.Box(r, "적 프리팹을 여기로 드래그");

        Event e = Event.current;
        if (!r.Contains(e.mousePosition)) return;

        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object o in DragAndDrop.objectReferences)
                {
                    GameObject go = o as GameObject;
                    if (go != null && PrefabUtility.IsPartOfPrefabAsset(go) && !prefabs.Contains(go))
                        prefabs.Add(go);
                }
                if (selected < 0 && prefabs.Count > 0) selected = 0;
                SavePrefabList();
            }
            e.Use();
        }
    }

    // ---------- Scene 뷰 ----------
    void OnSceneGUI(SceneView sv)
    {
        if (EditorApplication.isPlaying) return;
        if (!loaded || (!placing && !erasing)) return;

        GameObject root = FindPreviewRoot();
        if (root == null) { loaded = false; return; }

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            placing = false;
            erasing = false;
            Repaint();
            e.Use();
            return;
        }

        Vector3 raw = MouseToWorld(e.mousePosition);
        Vector3 pos = snap ? Snap(raw) : raw;

        if (placing)
        {
            Handles.color = new Color(0.2f, 1f, 0.4f, 0.5f);
            Handles.DrawSolidDisc(pos, Vector3.forward, 0.4f);
            string label = (selected >= 0 && selected < prefabs.Count) ? prefabs[selected].name : "프리팹을 선택하세요";
            Handles.Label(pos + Vector3.up * 0.7f, label);
        }
        else
        {
            Handles.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Handles.DrawWireDisc(raw, Vector3.forward, eraseRadius);
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sv.Repaint();

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (placing) Place(root, pos);
            else Erase(root, raw);
            Repaint();
            e.Use();
        }
    }

    Vector3 MouseToWorld(Vector2 mouse)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);   // 2D: z=0 평면
        float d;
        return plane.Raycast(ray, out d) ? ray.GetPoint(d) : ray.origin;
    }

    Vector3 Snap(Vector3 p)
    {
        return new Vector3(
            Mathf.Round(p.x / snapSize) * snapSize,
            Mathf.Round(p.y / snapSize) * snapSize,
            p.z);
    }

    // ---------- 배치 / 삭제 ----------
    void Place(GameObject root, Vector3 pos)
    {
        if (selected < 0 || selected >= prefabs.Count || prefabs[selected] == null)
        {
            Debug.LogWarning("[봇 에디터] 목록에서 적 프리팹을 먼저 선택하세요.");
            return;
        }

        foreach (Transform child in root.transform)                    // 같은 자리 중복 방지
            if (Vector2.Distance(child.position, pos) < 0.1f) return;

        GameObject prefab = prefabs[selected];
        SpawnPreview(root, prefab,
            new Vector3(pos.x, pos.y, prefab.transform.position.z),
            prefab.transform.eulerAngles.z,
            prefab.transform.localScale);
    }

    void Erase(GameObject root, Vector3 pos)
    {
        Transform nearest = null;
        float best = eraseRadius;
        foreach (Transform child in root.transform)
        {
            float d = Vector2.Distance(child.position, pos);
            if (d <= best) { best = d; nearest = child; }
        }
        if (nearest != null) DestroyImmediate(nearest.gameObject);
    }

    GameObject SpawnPreview(GameObject root, GameObject prefab, Vector3 pos, float rotZ, Vector3 scale)
    {
        if (prefab == null) return null;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
        if (scale != Vector3.zero) go.transform.localScale = scale;

        // 씬 파일에 저장되지 않게 표시
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSave;
        return go;
    }

    // ---------- 불러오기 / 저장 ----------
    void LoadStage()
    {
        GameObject old = FindPreviewRoot();
        if (old != null && Snapshot(old) != lastSnapshot &&
            !EditorUtility.DisplayDialog("봇 에디터", "저장하지 않은 배치가 있습니다. 버리고 불러올까요?", "버리고 불러오기", "취소"))
            return;
        if (old != null) DestroyImmediate(old);

        loadedStage = stage;
        loadedSub = subStage;
        currentLayout = FindLayout(stage, subStage);

        GameObject root = new GameObject(PREVIEWROOT);
        root.hideFlags = HideFlags.DontSave;

        if (currentLayout != null)
            foreach (BOTLAYOUT.ENTRY e in currentLayout.entries)
                SpawnPreview(root, e.prefab, e.position, e.rotationZ, e.scale);

        loaded = true;
        lastSnapshot = Snapshot(root);
    }

    void SaveLayout()
    {
        GameObject root = FindPreviewRoot();
        if (root == null) { Debug.LogWarning("[봇 에디터] 불러온 미리보기가 없습니다."); return; }

        if (currentLayout == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(LAYOUTFOLDER)) AssetDatabase.CreateFolder("Assets/Resources", "BOTLAYOUTS");

            currentLayout = CreateInstance<BOTLAYOUT>();
            currentLayout.stage = loadedStage;
            currentLayout.subStage = loadedSub;
            AssetDatabase.CreateAsset(currentLayout, LAYOUTFOLDER + "/BOTLAYOUT_" + loadedStage + "_" + loadedSub + ".asset");
        }

        currentLayout.entries.Clear();
        foreach (Transform child in root.transform)
        {
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            if (prefab == null) continue;

            currentLayout.entries.Add(new BOTLAYOUT.ENTRY
            {
                prefab = prefab,
                position = child.position,
                rotationZ = child.eulerAngles.z,
                scale = child.localScale
            });
        }

        EditorUtility.SetDirty(currentLayout);
        AssetDatabase.SaveAssets();
        lastSnapshot = Snapshot(root);
        Debug.Log("[봇 에디터] " + loadedStage + "-" + loadedSub + " 저장 완료: " + currentLayout.entries.Count + "마리");
    }

    BOTLAYOUT FindLayout(int s, int sub)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BOTLAYOUT"))
        {
            BOTLAYOUT l = AssetDatabase.LoadAssetAtPath<BOTLAYOUT>(AssetDatabase.GUIDToAssetPath(guid));
            if (l != null && l.stage == s && l.subStage == sub) return l;
        }
        return null;
    }

    void ConfirmClear()
    {
        GameObject root = FindPreviewRoot();
        if (root == null) { loaded = false; return; }

        if (Snapshot(root) != lastSnapshot &&
            !EditorUtility.DisplayDialog("봇 에디터", "저장하지 않은 배치가 사라집니다. 지울까요?", "지우기", "취소"))
            return;

        DestroyImmediate(root);
        loaded = false;
    }

    GameObject FindPreviewRoot()
    {
        foreach (GameObject g in SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == PREVIEWROOT) return g;
        return null;
    }

    // 변경 여부를 비교하기 위한 배치 요약 문자열
    string Snapshot(GameObject root)
    {
        var sb = new StringBuilder();
        foreach (Transform c in root.transform)
            sb.Append(c.name).Append(c.position.ToString("F2"))
              .Append(c.eulerAngles.z.ToString("F1"))
              .Append(c.localScale.ToString("F2")).Append(';');
        return sb.ToString();
    }
}