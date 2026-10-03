using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenerEditor
{
    /// <summary>
    /// 스테이지 제작용 맵 에디터. (Tools > Gardener > Map Editor)
    /// ※ 개발 편의용 에디터 도구이며 게임에는 포함되지 않는다. (Assets/Editor 폴더 = 빌드 제외)
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        private enum MapTool { Select, Platform, OneWay, Erase, Start, Goal }

        private static readonly string[] ToolLabels = { "선택", "발판", "통과 발판", "지우개", "시작점", "골" };
        private static readonly float[] GridSizes = { 0.25f, 0.5f, 1f, 2f };
        private static readonly string[] GridLabels = { "0.25", "0.5", "1 (플레이어 크기)", "2" };

        [SerializeField] private GameObject stageRoot;
        [SerializeField] private Sprite platformSprite;
        [SerializeField] private MapTool tool = MapTool.Select;
        [SerializeField] private int gridIndex = 2;

        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool showStageBounds = true;
        [SerializeField] private bool showCameraFrame = true;
        [SerializeField] private bool showJumpGuide = false;

        [SerializeField] private Vector2 padX = Vector2.zero;
        [SerializeField] private Vector2 padY = new Vector2(0f, 3f);
        [SerializeField] private float aspect = 16f / 9f;

        private Vector2 scroll;
        private string infoMessage;

        // 드래그 상태
        private bool dragging;
        private Vector2 dragStart;
        private Vector2 dragCurrent;
        private int eraseUndoGroup;

        // 마우스 월드 좌표
        private Vector2 mouseWorld;
        private bool mouseValid;

        private List<MapIssue> issues;

        private JumpStats statsCache;
        private double statsTime = -10.0;

        [MenuItem("Tools/Gardener/Map Editor")]
        public static void Open()
        {
            MapEditorWindow w = GetWindow<MapEditorWindow>("Map Editor");
            w.minSize = new Vector2(300f, 420f);
            w.Show();
        }

        // ================================================================ lifecycle

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;

            if (tool != MapTool.Select && Tools.current == Tool.None)
                Tools.current = Tool.Move;
        }

        private void OnUndoRedo()
        {
            statsTime = -10.0;
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnHierarchyChanged()
        {
            statsTime = -10.0;
            Repaint();
        }

        private GameObject StageRoot
        {
            get
            {
                if (stageRoot == null) stageRoot = MapEditorUtil.FindStageRoot();
                return stageRoot;
            }
        }

        private float Grid
        {
            get { return GridSizes[Mathf.Clamp(gridIndex, 0, GridSizes.Length - 1)]; }
        }

        private Sprite ResolveSprite(GameObject stage)
        {
            if (platformSprite != null) return platformSprite;
            return MapEditorUtil.FindDefaultSprite(stage);
        }

        private JumpStats GetStats()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - statsTime > 0.5)
            {
                statsCache = MapEditorJump.Read();
                statsTime = now;
            }
            return statsCache;
        }

        private void SetTool(MapTool newTool)
        {
            if (newTool == tool) return;

            tool = newTool;
            dragging = false;

            // 그리기 도구 중에는 Unity 기본 이동/회전 핸들을 숨긴다.
            if (tool != MapTool.Select) Tools.current = Tool.None;
            else if (Tools.current == Tool.None) Tools.current = Tool.Move;

            SceneView.RepaintAll();
        }

        // ================================================================ window GUI

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 만든 내용은 플레이를 멈추면 사라집니다. 맵 편집은 플레이를 멈추고 하세요.", MessageType.Warning);
            }

            DrawStageSection();

            GameObject stage = StageRoot;
            if (stage != null)
            {
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    DrawToolSection(stage);
                }
                DrawViewSection();
                DrawSelectionSection(stage);
                DrawPlayerCameraSection(stage);
                DrawJumpSection();
                DrawValidateSection(stage);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawStageSection()
        {
            EditorGUILayout.LabelField("스테이지", EditorStyles.boldLabel);

            GameObject current = StageRoot;
            GameObject picked = (GameObject)EditorGUILayout.ObjectField("Stage Root", current, typeof(GameObject), true);
            if (picked != current) stageRoot = picked;

            if (StageRoot == null)
            {
                EditorGUILayout.HelpBox("씬에 'Stage' 오브젝트가 없습니다. 새로 만들면 Platforms / Markers 그룹이 같이 생성됩니다.", MessageType.Info);
                if (GUILayout.Button("새 Stage 만들기"))
                {
                    stageRoot = MapEditorUtil.CreateStageRoot();
                    Selection.activeGameObject = stageRoot;
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawToolSection(GameObject stage)
        {
            EditorGUILayout.LabelField("도구", EditorStyles.boldLabel);

            int sel = GUILayout.SelectionGrid((int)tool, ToolLabels, 3);
            if (sel != (int)tool) SetTool((MapTool)sel);

            EditorGUILayout.HelpBox(ToolHelp(tool), MessageType.None);

            gridIndex = EditorGUILayout.Popup("그리드 크기", gridIndex, GridLabels);

            platformSprite = (Sprite)EditorGUILayout.ObjectField("발판 스프라이트", platformSprite, typeof(Sprite), false);
            if (platformSprite == null && ResolveSprite(stage) == null)
            {
                EditorGUILayout.HelpBox("기본 스프라이트를 찾지 못했습니다. 위 칸에 발판용 스프라이트를 지정하세요. (비워두면 발판이 안 보입니다)", MessageType.Warning);
            }
            EditorGUILayout.Space();
        }

        private static string ToolHelp(MapTool t)
        {
            switch (t)
            {
                case MapTool.Platform:
                    return "Scene 뷰에서 드래그하면 사각형 발판이 만들어집니다. 클릭 한 번 = 1칸.\nEsc = 취소 / Alt+드래그 = 화면 이동";
                case MapTool.OneWay:
                    return "아래에서 위로 뚫고 올라갈 수 있는 발판. 사용법은 발판과 같습니다.";
                case MapTool.Erase:
                    return "발판을 클릭(또는 드래그)해서 삭제합니다. Ctrl+Z로 되돌릴 수 있습니다.";
                case MapTool.Start:
                    return "클릭한 칸에 플레이어 시작점을 놓습니다. (스테이지에 1개만 존재, 다시 클릭하면 이동)";
                case MapTool.Goal:
                    return "클릭한 칸에 골 지점을 놓습니다. (1개만 존재)";
                default:
                    return "Unity 기본 선택/이동 모드입니다. 위 도구를 고르면 Scene 뷰에서 바로 그릴 수 있습니다.\n(Q/W/E/R 키를 누르면 그리기 도구가 꺼집니다)";
            }
        }

        private void DrawViewSection()
        {
            EditorGUILayout.LabelField("Scene 뷰 표시", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            showGrid = EditorGUILayout.ToggleLeft("그리드", showGrid);
            showStageBounds = EditorGUILayout.ToggleLeft("스테이지 영역 / 크기", showStageBounds);
            showCameraFrame = EditorGUILayout.ToggleLeft("시작점 기준 카메라 시야", showCameraFrame);
            showJumpGuide = EditorGUILayout.ToggleLeft("점프 도달 범위 (마우스 위치 기준)", showJumpGuide);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

            EditorGUILayout.Space();
        }

        private void DrawSelectionSection(GameObject stage)
        {
            EditorGUILayout.LabelField("선택한 발판", EditorStyles.boldLabel);
            if (GUILayout.Button("그리드에 모서리 맞추기"))
            {
                int n = MapEditorUtil.SnapSelectedPlatforms(stage, Grid);
                infoMessage = n > 0 ? n + "개 발판을 그리드에 맞췄습니다." : "Platforms 아래의 발판을 먼저 선택하세요.";
            }
            EditorGUILayout.Space();
        }

        private void DrawPlayerCameraSection(GameObject stage)
        {
            EditorGUILayout.LabelField("플레이어 / 카메라", EditorStyles.boldLabel);

            if (GUILayout.Button("플레이어·카메라를 시작점에 맞추기"))
            {
                bool ok = MapEditorUtil.AlignPlayerAndCameraToStart(stage);
                infoMessage = ok ? "플레이어와 카메라를 시작점으로 옮겼습니다." : "시작점(PlayerStart)이 없습니다.";
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("카메라 한계 (스테이지 크기 기준)", EditorStyles.miniBoldLabel);
            padX = EditorGUILayout.Vector2Field("여백 좌 / 우", padX);
            padY = EditorGUILayout.Vector2Field("여백 하 / 상", padY);
            aspect = EditorGUILayout.FloatField("화면 비율 (가로/세로)", aspect);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("한계 적용"))
            {
                string msg;
                MapEditorUtil.ApplyCameraLimits(stage, padX, padY, aspect, out msg);
                infoMessage = msg;
            }
            if (GUILayout.Button("한계 해제"))
            {
                MapEditorUtil.ClearCameraLimits();
                infoMessage = "카메라 한계를 껐습니다.";
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(infoMessage))
                EditorGUILayout.HelpBox(infoMessage, MessageType.Info);

            EditorGUILayout.Space();
        }

        private void DrawJumpSection()
        {
            EditorGUILayout.LabelField("점프 수치 (맵 설계 참고용)", EditorStyles.boldLabel);

            JumpStats s = GetStats();
            if (!s.valid)
            {
                EditorGUILayout.HelpBox("씬에서 PlayerController2D를 찾지 못했습니다.", MessageType.Info);
                EditorGUILayout.Space();
                return;
            }

            EditorGUILayout.LabelField("점프 높이", s.jumpHeight.ToString("0.0") + " 칸");
            EditorGUILayout.LabelField("점프 거리 (같은 높이)", s.jumpRange.ToString("0.0") + " 칸");
            EditorGUILayout.LabelField("이단 점프 높이 / 거리", s.doubleHeight.ToString("0.0") + " / " + s.doubleRange.ToString("0.0") + " 칸");
            EditorGUILayout.LabelField("점프+공중 대시 거리", s.dashJumpRange.ToString("0.0") + " 칸");
            EditorGUILayout.LabelField("지상 대시 거리", s.groundDashDistance.ToString("0.0") + " 칸");
            EditorGUILayout.HelpBox("1칸 = 1유닛 = 플레이어 크기. 점프 키를 계속 누른 최대치 기준의 근사값입니다. " +
                                    "여유 있게 설계하려면 거리/높이를 이 값의 70~80%로 잡으세요. (이단 점프·대시는 강화 획득 후 가능)",
                MessageType.None);
            EditorGUILayout.Space();
        }

        private void DrawValidateSection(GameObject stage)
        {
            EditorGUILayout.LabelField("스테이지 검사", EditorStyles.boldLabel);

            if (GUILayout.Button("검사 실행"))
            {
                issues = MapEditorValidator.Run(stage);
            }

            if (issues == null) return;

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("문제를 찾지 못했습니다.", MessageType.Info);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                MapIssue issue = issues[i];
                MessageType mt = issue.severity == IssueSeverity.Error ? MessageType.Error
                    : issue.severity == IssueSeverity.Warning ? MessageType.Warning
                    : MessageType.Info;

                EditorGUILayout.HelpBox(issue.message, mt);

                EditorGUILayout.BeginHorizontal();
                if (issue.target != null && GUILayout.Button("선택", GUILayout.Width(50f)))
                {
                    Selection.activeObject = issue.target;
                    EditorGUIUtility.PingObject(issue.target);
                }
                if (issue.fix != null && GUILayout.Button(issue.fixLabel))
                {
                    issue.fix();
                    issues = MapEditorValidator.Run(stage);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ================================================================ scene view

        private void OnSceneGUI(SceneView sv)
        {
            GameObject stage = StageRoot;
            if (stage == null) return;

            sv.wantsMouseMove = true;
            Event e = Event.current;

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                Vector2 w;
                mouseValid = TryGetMouseWorld(sv, e.mousePosition, out w);
                if (mouseValid) mouseWorld = w;
                if (e.type == EventType.MouseMove) sv.Repaint();
            }

            if (e.type == EventType.Repaint) DrawOverlays(sv, stage);

            if (Application.isPlaying) return;

            // Q/W/E/R 등으로 Unity 기본 도구를 고르면 그리기 도구는 끈다.
            if (tool != MapTool.Select && Tools.current != Tool.None)
            {
                tool = MapTool.Select;
                dragging = false;
                Repaint();
            }

            if (tool != MapTool.Select) HandleTool(sv, stage, e);
        }

        private void HandleTool(SceneView sv, GameObject stage, Event e)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            EventType type = e.GetTypeForControl(id);

            Vector2 world;
            bool hasWorld = TryGetMouseWorld(sv, e.mousePosition, out world);

            switch (type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseDown:
                    if (e.button != 0 || e.alt || !hasWorld) break;
                    GUIUtility.hotControl = id;
                    e.Use();
                    BeginDrag(world, stage);
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    e.Use();
                    if (hasWorld) UpdateDrag(world, stage);
                    sv.Repaint();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id || e.button != 0) break;
                    GUIUtility.hotControl = 0;
                    e.Use();
                    EndDrag(stage);
                    sv.Repaint();
                    Repaint();
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape && dragging)
                    {
                        if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0;
                        dragging = false;
                        e.Use();
                        sv.Repaint();
                    }
                    break;
            }
        }

        private void BeginDrag(Vector2 world, GameObject stage)
        {
            dragging = true;
            dragStart = world;
            dragCurrent = world;

            if (tool == MapTool.Erase)
            {
                Undo.IncrementCurrentGroup();
                eraseUndoGroup = Undo.GetCurrentGroup();
                MapEditorUtil.EraseAt(stage, world);
            }
        }

        private void UpdateDrag(Vector2 world, GameObject stage)
        {
            dragCurrent = world;
            if (tool == MapTool.Erase) MapEditorUtil.EraseAt(stage, world);
        }

        private void EndDrag(GameObject stage)
        {
            if (!dragging) return;
            dragging = false;

            float g = Grid;

            switch (tool)
            {
                case MapTool.Platform:
                case MapTool.OneWay:
                {
                    Rect r = MapEditorUtil.RectFromDrag(dragStart, dragCurrent, g);
                    MapEditorUtil.CreatePlatform(stage, r, tool == MapTool.OneWay, ResolveSprite(stage));
                    break;
                }

                case MapTool.Erase:
                {
                    Undo.SetCurrentGroupName("Erase Platforms");
                    Undo.CollapseUndoOperations(eraseUndoGroup);
                    break;
                }

                case MapTool.Start:
                {
                    Vector2 p = MapEditorUtil.SnapBody(dragCurrent, g);
                    Transform t = MapEditorUtil.PlaceMarker(stage, MapNames.PlayerStart, p);
                    MapEditorUtil.LinkRespawn(t);
                    break;
                }

                case MapTool.Goal:
                {
                    Vector2 p = MapEditorUtil.SnapBody(dragCurrent, g);
                    MapEditorUtil.PlaceMarker(stage, MapNames.Goal, p);
                    break;
                }
            }
        }

        // ================================================================ overlays

        private void DrawOverlays(SceneView sv, GameObject stage)
        {
            CompareFunction prevZ = Handles.zTest;
            Handles.zTest = CompareFunction.Always;

            float g = Grid;

            if (showGrid) DrawGrid(sv, g);
            if (showStageBounds) DrawStageBounds(stage);
            if (showCameraFrame) DrawCameraFrame(stage);
            DrawToolPreview(stage, g);
            if (showJumpGuide && mouseValid) DrawJumpGuide(g);

            Handles.zTest = prevZ;
        }

        private static void DrawRect(Rect r, Color face, Color outline)
        {
            Vector3[] v = new Vector3[]
            {
                new Vector3(r.xMin, r.yMin, 0f),
                new Vector3(r.xMin, r.yMax, 0f),
                new Vector3(r.xMax, r.yMax, 0f),
                new Vector3(r.xMax, r.yMin, 0f)
            };
            Handles.DrawSolidRectangleWithOutline(v, face, outline);
        }

        private static void DrawGrid(SceneView sv, float g)
        {
            Vector2 a;
            Vector2 b;
            if (!TryGetMouseWorld(sv, Vector2.zero, out a)) return;
            if (!TryGetMouseWorld(sv, new Vector2(sv.position.width, sv.position.height), out b)) return;

            float minX = Mathf.Min(a.x, b.x);
            float maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y);
            float maxY = Mathf.Max(a.y, b.y);
            if (float.IsNaN(minX) || float.IsNaN(maxX) || float.IsNaN(minY) || float.IsNaN(maxY)) return;

            // 멀리 축소했을 때 선이 너무 많아지지 않도록 간격을 키운다.
            float step = g;
            for (int guard = 0; guard < 30; guard++)
            {
                if ((maxX - minX) / step <= 120f && (maxY - minY) / step <= 120f) break;
                step *= 2f;
            }

            Handles.color = new Color(1f, 1f, 1f, 0.10f);

            float startX = Mathf.Floor(minX / step) * step;
            int nx = Mathf.Min(300, Mathf.CeilToInt((maxX - startX) / step));
            for (int i = 0; i <= nx; i++)
            {
                float x = startX + i * step;
                Handles.DrawLine(new Vector3(x, minY, 0f), new Vector3(x, maxY, 0f));
            }

            float startY = Mathf.Floor(minY / step) * step;
            int ny = Mathf.Min(300, Mathf.CeilToInt((maxY - startY) / step));
            for (int i = 0; i <= ny; i++)
            {
                float y = startY + i * step;
                Handles.DrawLine(new Vector3(minX, y, 0f), new Vector3(maxX, y, 0f));
            }

            // 원점 축
            Handles.color = new Color(1f, 1f, 1f, 0.25f);
            Handles.DrawLine(new Vector3(0f, minY, 0f), new Vector3(0f, maxY, 0f));
            Handles.DrawLine(new Vector3(minX, 0f, 0f), new Vector3(maxX, 0f, 0f));
        }

        private static void DrawStageBounds(GameObject stage)
        {
            Rect r;
            if (!MapEditorUtil.TryGetStageBounds(stage, out r)) return;

            DrawRect(r, new Color(0f, 0f, 0f, 0f), new Color(1f, 0.9f, 0.3f, 0.7f));
            Handles.Label(new Vector3(r.xMin, r.yMax + 0.4f, 0f),
                string.Format("스테이지 {0:0.##} x {1:0.##}", r.width, r.height), EditorStyles.whiteBoldLabel);
        }

        private void DrawCameraFrame(GameObject stage)
        {
            Transform start = MapEditorUtil.FindMarker(stage, MapNames.PlayerStart);
            if (start == null) return;

            Camera cam = Camera.main;
            float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
            float halfW = halfH * Mathf.Max(0.1f, aspect);

            Vector3 c = start.position;
            Vector3 p0 = new Vector3(c.x - halfW, c.y - halfH, 0f);
            Vector3 p1 = new Vector3(c.x - halfW, c.y + halfH, 0f);
            Vector3 p2 = new Vector3(c.x + halfW, c.y + halfH, 0f);
            Vector3 p3 = new Vector3(c.x + halfW, c.y - halfH, 0f);

            Handles.color = new Color(1f, 1f, 1f, 0.45f);
            Handles.DrawDottedLine(p0, p1, 4f);
            Handles.DrawDottedLine(p1, p2, 4f);
            Handles.DrawDottedLine(p2, p3, 4f);
            Handles.DrawDottedLine(p3, p0, 4f);
        }

        private void DrawToolPreview(GameObject stage, float g)
        {
            if (tool == MapTool.Select || !mouseValid || Application.isPlaying) return;

            switch (tool)
            {
                case MapTool.Platform:
                case MapTool.OneWay:
                {
                    Rect r = dragging
                        ? MapEditorUtil.RectFromDrag(dragStart, dragCurrent, g)
                        : MapEditorUtil.CellAt(mouseWorld, g);

                    Color baseColor = tool == MapTool.OneWay ? MapEditorUtil.OneWayColor : MapEditorUtil.SolidColor;
                    DrawRect(r, new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f), new Color(1f, 1f, 1f, 0.9f));

                    if (dragging)
                    {
                        Handles.Label(new Vector3(r.xMin, r.yMax + 0.3f, 0f),
                            string.Format("{0:0.##} x {1:0.##}", r.width, r.height), EditorStyles.whiteBoldLabel);
                    }
                    break;
                }

                case MapTool.Erase:
                {
                    Transform t = MapEditorUtil.FindPlatformAt(stage, mouseWorld);
                    if (t != null)
                        DrawRect(MapEditorUtil.GetRect(t), new Color(1f, 0.2f, 0.2f, 0.35f), new Color(1f, 0.3f, 0.3f, 1f));
                    break;
                }

                case MapTool.Start:
                case MapTool.Goal:
                {
                    Vector2 p = MapEditorUtil.SnapBody(dragging ? dragCurrent : mouseWorld, g);
                    Color c = tool == MapTool.Start ? new Color(0.3f, 1f, 0.4f, 1f) : new Color(1f, 0.4f, 0.3f, 1f);
                    DrawRect(new Rect(p.x - 0.5f, p.y - 0.5f, 1f, 1f), new Color(c.r, c.g, c.b, 0.3f), c);
                    break;
                }
            }
        }

        private void DrawJumpGuide(float g)
        {
            JumpStats s = GetStats();
            if (!s.valid) return;

            // 마우스 위치 = 플레이어 발 위치 (그리드에 맞춤). 몸 중심은 0.5 위.
            Vector2 feet = new Vector2(MapEditorUtil.Snap(mouseWorld.x, g), MapEditorUtil.Snap(mouseWorld.y, g));
            Vector2 pivot = feet + Vector2.up * 0.5f;

            Handles.color = new Color(1f, 1f, 1f, 0.7f);
            Handles.DrawWireCube(new Vector3(pivot.x, pivot.y, 0f), new Vector3(1f, 1f, 0f));

            Color cJump = new Color(0.2f, 0.9f, 1f, 0.95f);
            Color cDouble = new Color(1f, 0.9f, 0.2f, 0.95f);
            Color cDash = new Color(1f, 0.4f, 0.9f, 0.95f);

            for (int dir = -1; dir <= 1; dir += 2)
            {
                DrawArc(MapEditorJump.Simulate(s, pivot, dir, false, false, 3f), cJump);
                DrawArc(MapEditorJump.Simulate(s, pivot, dir, true, false, 3f), cDouble);
                DrawArc(MapEditorJump.Simulate(s, pivot, dir, false, true, 3f), cDash);
            }

            Handles.color = cJump;
            Handles.Label(new Vector3(pivot.x + s.jumpRange, pivot.y + 0.2f, 0f), "점프 " + s.jumpRange.ToString("0.0"), EditorStyles.whiteBoldLabel);
            Handles.Label(new Vector3(pivot.x + s.doubleRange, pivot.y + 0.7f, 0f), "이단 " + s.doubleRange.ToString("0.0"), EditorStyles.whiteBoldLabel);
            Handles.Label(new Vector3(pivot.x + s.dashJumpRange, pivot.y + 1.2f, 0f), "대시 " + s.dashJumpRange.ToString("0.0"), EditorStyles.whiteBoldLabel);
        }

        private static void DrawArc(List<Vector3> pts, Color color)
        {
            if (pts == null || pts.Count < 2) return;
            Handles.color = color;
            Handles.DrawAAPolyLine(2.5f, pts.ToArray());
        }

        // ================================================================ helpers

        private static bool TryGetMouseWorld(SceneView sv, Vector2 guiPos, out Vector2 world)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPos);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);

            float dist;
            if (plane.Raycast(ray, out dist))
            {
                Vector3 p = ray.GetPoint(dist);
                world = new Vector2(p.x, p.y);
                return true;
            }

            if (sv != null && sv.orthographic)
            {
                world = new Vector2(ray.origin.x, ray.origin.y);
                return true;
            }

            world = Vector2.zero;
            return false;
        }
    }
}
