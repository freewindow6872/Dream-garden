using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GardenerEditor
{
    /// <summary>
    /// 맵 에디터가 쓰는 씬 구조 규칙. (에디터 전용 - 게임 빌드에는 포함되지 않음)
    ///
    ///   Stage
    ///    ├ Platforms   : 발판들 (Ground 레이어 + BoxCollider2D)
    ///    └ Markers     : PlayerStart, Goal
    /// </summary>
    public static class MapNames
    {
        public const string Stage = "Stage";
        public const string Platforms = "Platforms";
        public const string Markers = "Markers";
        public const string PlayerStart = "PlayerStart";
        public const string Goal = "Goal";
    }

    public static class MapEditorUtil
    {
        /// <summary>2D 패키지 기본 Square 스프라이트 (Create > 2D > Sprites > Square 가 쓰는 것)</summary>
        private const string DefaultSquareGuid = "311925a002f4447b3a28927169b83ea6";

        public static readonly Color SolidColor = new Color(0.35f, 0.62f, 0.32f, 1f);
        public static readonly Color OneWayColor = new Color(0.78f, 0.68f, 0.35f, 1f);

        // ------------------------------------------------------------ stage root

        public static GameObject FindStageRoot()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == MapNames.Stage) return roots[i];
            }
            return null;
        }

        public static GameObject CreateStageRoot()
        {
            GameObject root = new GameObject(MapNames.Stage);
            Undo.RegisterCreatedObjectUndo(root, "Create Stage");
            GetGroup(root, MapNames.Platforms, true);
            GetGroup(root, MapNames.Markers, true);
            EditorSceneManager.MarkSceneDirty(root.scene);
            return root;
        }

        public static Transform GetGroup(GameObject stage, string groupName, bool create)
        {
            Transform t = stage.transform.Find(groupName);
            if (t != null || !create) return t;

            GameObject g = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(g, "Create " + groupName);
            g.transform.SetParent(stage.transform, false);
            return g.transform;
        }

        public static Transform FindMarker(GameObject stage, string markerName)
        {
            if (stage == null) return null;
            Transform markers = GetGroup(stage, MapNames.Markers, false);
            return markers != null ? markers.Find(markerName) : null;
        }

        // ------------------------------------------------------------ snapping

        public static float Snap(float v, float grid)
        {
            return Mathf.Round(v / grid) * grid;
        }

        /// <summary>점이 속한 그리드 칸</summary>
        public static Rect CellAt(Vector2 p, float grid)
        {
            float x = Mathf.Floor(p.x / grid) * grid;
            float y = Mathf.Floor(p.y / grid) * grid;
            return new Rect(x, y, grid, grid);
        }

        /// <summary>드래그 시작 칸 ~ 끝 칸을 모두 포함하는 사각형</summary>
        public static Rect RectFromDrag(Vector2 a, Vector2 b, float grid)
        {
            Rect ca = CellAt(a, grid);
            Rect cb = CellAt(b, grid);
            return Rect.MinMaxRect(
                Mathf.Min(ca.xMin, cb.xMin),
                Mathf.Min(ca.yMin, cb.yMin),
                Mathf.Max(ca.xMax, cb.xMax),
                Mathf.Max(ca.yMax, cb.yMax));
        }

        /// <summary>플레이어 몸(1x1)이 그리드 칸에 딱 맞도록 마커 위치를 맞춘다.</summary>
        public static Vector2 SnapBody(Vector2 p, float grid)
        {
            return new Vector2(Snap(p.x - 0.5f, grid) + 0.5f, Snap(p.y - 0.5f, grid) + 0.5f);
        }

        // ------------------------------------------------------------ platforms

        /// <summary>발판의 월드 사각형 (콜라이더 기준, 회전은 무시)</summary>
        public static Rect GetRect(Transform t)
        {
            Vector3 s = t.lossyScale;
            Vector2 size;
            Vector2 center;

            BoxCollider2D box = t.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                size = new Vector2(Mathf.Abs(s.x * box.size.x), Mathf.Abs(s.y * box.size.y));
                center = t.TransformPoint(box.offset);
            }
            else
            {
                size = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
                center = t.position;
            }

            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        public static bool IsOneWay(Transform t)
        {
            return t.GetComponent<PlatformEffector2D>() != null;
        }

        public static Transform FindPlatformAt(GameObject stage, Vector2 p)
        {
            Transform platforms = GetGroup(stage, MapNames.Platforms, false);
            if (platforms == null) return null;

            for (int i = platforms.childCount - 1; i >= 0; i--)
            {
                Transform c = platforms.GetChild(i);
                if (GetRect(c).Contains(p)) return c;
            }
            return null;
        }

        public static bool TryGetStageBounds(GameObject stage, out Rect bounds)
        {
            bounds = new Rect();
            Transform platforms = GetGroup(stage, MapNames.Platforms, false);
            if (platforms == null || platforms.childCount == 0) return false;

            bool first = true;
            for (int i = 0; i < platforms.childCount; i++)
            {
                Rect r = GetRect(platforms.GetChild(i));
                if (first)
                {
                    bounds = r;
                    first = false;
                }
                else
                {
                    bounds = Rect.MinMaxRect(
                        Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin),
                        Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax));
                }
            }
            return true;
        }

        public static Sprite FindDefaultSprite(GameObject stage)
        {
            string path = AssetDatabase.GUIDToAssetPath(DefaultSquareGuid);
            if (!string.IsNullOrEmpty(path))
            {
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }

            // 기본 Square를 못 찾으면 스테이지 안에 이미 쓰는 스프라이트를 빌려 쓴다.
            if (stage != null)
            {
                SpriteRenderer[] srs = stage.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < srs.Length; i++)
                {
                    if (srs[i].sprite != null) return srs[i].sprite;
                }
            }
            return null;
        }

        public static GameObject CreatePlatform(GameObject stage, Rect rect, bool oneWay, Sprite sprite)
        {
            Transform parent = GetGroup(stage, MapNames.Platforms, true);

            GameObject go = new GameObject(oneWay ? "OneWayPlatform" : "Platform");
            Undo.RegisterCreatedObjectUndo(go, oneWay ? "Create One-Way Platform" : "Create Platform");
            go.transform.SetParent(parent, false);
            go.layer = GameLayers.Ground;
            go.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
            go.transform.localScale = new Vector3(rect.width, rect.height, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = oneWay ? OneWayColor : SolidColor;
            sr.sortingOrder = -10;

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;

            if (oneWay)
            {
                box.usedByEffector = true;
                PlatformEffector2D effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 170f;
            }

            GameObjectUtility.EnsureUniqueNameForSibling(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return go;
        }

        public static bool EraseAt(GameObject stage, Vector2 p)
        {
            Transform t = FindPlatformAt(stage, p);
            if (t == null) return false;

            Scene scene = t.gameObject.scene;
            Undo.DestroyObjectImmediate(t.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        /// <summary>선택된 발판의 모서리를 그리드에 맞춘다.</summary>
        public static int SnapSelectedPlatforms(GameObject stage, float grid)
        {
            Transform platforms = GetGroup(stage, MapNames.Platforms, false);
            if (platforms == null) return 0;

            int count = 0;
            Transform[] selected = Selection.transforms;
            for (int i = 0; i < selected.Length; i++)
            {
                Transform t = selected[i];
                if (t == platforms || !t.IsChildOf(platforms)) continue;

                Rect r = GetRect(t);
                if (r.width <= 0.0001f || r.height <= 0.0001f) continue;

                float minX = Snap(r.xMin, grid);
                float maxX = Snap(r.xMax, grid);
                float minY = Snap(r.yMin, grid);
                float maxY = Snap(r.yMax, grid);
                if (maxX - minX < grid) maxX = minX + grid;
                if (maxY - minY < grid) maxY = minY + grid;

                Rect snapped = Rect.MinMaxRect(minX, minY, maxX, maxY);

                Undo.RecordObject(t, "Snap Platform");
                Vector3 ls = t.localScale;
                t.localScale = new Vector3(ls.x * snapped.width / r.width, ls.y * snapped.height / r.height, ls.z);
                t.position += (Vector3)(snapped.center - r.center);
                count++;
            }

            if (count > 0) EditorSceneManager.MarkSceneDirty(stage.scene);
            return count;
        }

        // ------------------------------------------------------------ markers

        /// <summary>마커는 이름당 1개만 존재한다. 이미 있으면 이동.</summary>
        public static Transform PlaceMarker(GameObject stage, string markerName, Vector2 position)
        {
            Transform parent = GetGroup(stage, MapNames.Markers, true);
            Transform marker = parent.Find(markerName);

            if (marker != null)
            {
                Undo.RecordObject(marker, "Move " + markerName);
                marker.position = new Vector3(position.x, position.y, 0f);
            }
            else
            {
                GameObject go = new GameObject(markerName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + markerName);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(position.x, position.y, 0f);
                marker = go.transform;
            }

            EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
            return marker;
        }

        /// <summary>PlayerRespawn의 리스폰 지점이 비어 있으면 시작점으로 연결</summary>
        public static void LinkRespawn(Transform start)
        {
            PlayerRespawn pr = Object.FindObjectOfType<PlayerRespawn>();
            if (pr == null || start == null) return;

            SerializedObject so = new SerializedObject(pr);
            SerializedProperty p = so.FindProperty("respawnPoint");
            if (p == null || p.objectReferenceValue != null) return;

            p.objectReferenceValue = start;
            so.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------ player / camera

        public static bool AlignPlayerAndCameraToStart(GameObject stage)
        {
            Transform start = FindMarker(stage, MapNames.PlayerStart);
            if (start == null) return false;

            PlayerController2D pc = Object.FindObjectOfType<PlayerController2D>();
            if (pc != null)
            {
                Undo.RecordObject(pc.transform, "Move Player To Start");
                Vector3 pp = pc.transform.position;
                pc.transform.position = new Vector3(start.position.x, start.position.y, pp.z);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 offset = Vector2.zero;
                CameraFollow2D follow = cam.GetComponent<CameraFollow2D>();
                if (follow != null)
                {
                    SerializedProperty op = new SerializedObject(follow).FindProperty("offset");
                    if (op != null) offset = op.vector3Value;
                }

                Undo.RecordObject(cam.transform, "Move Camera To Start");
                float z = cam.transform.position.z;
                cam.transform.position = new Vector3(start.position.x + offset.x, start.position.y + offset.y, z);
            }

            EditorSceneManager.MarkSceneDirty(stage.scene);
            return true;
        }

        /// <summary>
        /// 스테이지(발판 전체) 크기에 맞춰 CameraFollow2D의 한계값을 계산한다.
        /// 카메라 "중심"이 움직일 수 있는 범위이므로, 화면 절반 크기만큼 안쪽으로 들어온다.
        /// </summary>
        public static bool ApplyCameraLimits(GameObject stage, Vector2 padX, Vector2 padY, float aspect, out string message)
        {
            Camera cam = Camera.main;
            CameraFollow2D follow = cam != null ? cam.GetComponent<CameraFollow2D>() : null;
            if (follow == null)
            {
                message = "MainCamera에 CameraFollow2D가 없습니다.";
                return false;
            }

            Rect bounds;
            if (!TryGetStageBounds(stage, out bounds))
            {
                message = "발판이 하나도 없어서 스테이지 크기를 알 수 없습니다.";
                return false;
            }

            float halfH = cam.orthographicSize;
            float halfW = halfH * Mathf.Max(0.1f, aspect);

            float left = bounds.xMin - padX.x;
            float right = bounds.xMax + padX.y;
            float bottom = bounds.yMin - padY.x;
            float top = bounds.yMax + padY.y;

            Vector2 min = new Vector2(left + halfW, bottom + halfH);
            Vector2 max = new Vector2(right - halfW, top - halfH);

            bool narrowX = min.x > max.x;
            bool narrowY = min.y > max.y;
            if (narrowX) { float cx = (left + right) * 0.5f; min.x = cx; max.x = cx; }
            if (narrowY) { float cy = (bottom + top) * 0.5f; min.y = cy; max.y = cy; }

            SerializedObject so = new SerializedObject(follow);
            SerializedProperty useLimits = so.FindProperty("useLimits");
            SerializedProperty minP = so.FindProperty("minPosition");
            SerializedProperty maxP = so.FindProperty("maxPosition");
            if (useLimits == null || minP == null || maxP == null)
            {
                message = "CameraFollow2D의 필드 이름이 바뀐 것 같습니다. (useLimits/minPosition/maxPosition)";
                return false;
            }

            useLimits.boolValue = true;
            minP.vector2Value = min;
            maxP.vector2Value = max;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(follow.gameObject.scene);

            message = string.Format("카메라 한계 적용: min({0:0.##}, {1:0.##})  max({2:0.##}, {3:0.##})", min.x, min.y, max.x, max.y);
            if (narrowX || narrowY)
                message += "\n※ 스테이지가 화면보다 작은 축은 가운데로 고정됩니다. (여백을 늘리면 카메라가 움직일 수 있습니다)";
            return true;
        }

        public static void ClearCameraLimits()
        {
            Camera cam = Camera.main;
            CameraFollow2D follow = cam != null ? cam.GetComponent<CameraFollow2D>() : null;
            if (follow == null) return;

            SerializedObject so = new SerializedObject(follow);
            SerializedProperty useLimits = so.FindProperty("useLimits");
            if (useLimits == null) return;

            useLimits.boolValue = false;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(follow.gameObject.scene);
        }
    }
}
