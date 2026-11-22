/*using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class STAGESELECTBUILDER
{
    const float REFW = 1920f;
    const float REFH = 1080f;

    // 정원(화면 중앙) 기준 점 위치. 1920x1080 기준이고, 나중에 씬에서 자유롭게 옮기면 됨
    static readonly Vector2[] POINTOFFSETS =
    {
        new Vector2(-145f,   -5f),
        new Vector2(-235f,   80f),
        new Vector2(-400f,    0f),
        new Vector2(-315f, -185f),
        new Vector2(-525f, -205f),
        new Vector2(-800f,  160f),
        new Vector2(-850f,  -85f),
        new Vector2(-750f, -375f)
    };

    [MenuItem("Tools/스테이지 선택 맵 자동 만들기")]
    static void Build()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!EditorUtility.DisplayDialog("스테이지 선택 맵",
            "현재 열린 씬 [" + sceneName + "]에 지도 UI를 만듭니다.\nSTAGESELECT 씬이 맞나요?",
            "만들기", "취소"))
            return;

        // ---- Canvas ----
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(cgo, "Canvas 생성");
            Debug.LogWarning("[스테이지맵] Canvas가 없어 새로 만들었습니다. EventSystem이 없으면 UI > Event System을 추가하세요.");
        }

        // ---- 이전에 자동 생성한 것이 있으면 지우고 다시 만듦 ----
        Transform oldUI = canvas.transform.Find("STAGEMAPUI");
        STAGEMAP oldMap = Object.FindObjectOfType<STAGEMAP>();
        if (oldUI != null || oldMap != null)
        {
            if (!EditorUtility.DisplayDialog("스테이지 선택 맵", "이미 만들어진 지도가 있습니다. 지우고 다시 만들까요?", "다시 만들기", "취소"))
                return;
            if (oldUI != null) Undo.DestroyObjectImmediate(oldUI.gameObject);
            if (oldMap != null)
            {
                if (oldMap.gameObject.name == "STAGEMAP") Undo.DestroyObjectImmediate(oldMap.gameObject);
                else Undo.DestroyObjectImmediate(oldMap);
            }
        }


        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Color dark = new Color(0.12f, 0.12f, 0.12f, 1f);

        // ---- 루트 (기존 UI보다 아래에 그려지도록 맨 앞 순서) ----
        RectTransform root = NewRect("STAGEMAPUI", canvas.transform);
        Stretch(root);
        root.SetAsFirstSibling();

        // ---- 영역 배경 (러프한 단색 사각형) ----
        Image baseBg = NewImage("BASE_STAGE4", root, new Color32(0xB8, 0xB8, 0xB8, 255), null);
        Stretch(baseBg.rectTransform);

        Image left = NewImage("REGION_LEFT_STAGE1", root, new Color32(0xD8, 0xD8, 0xD8, 255), null);
        Anchor(left.rectTransform, new Vector2(0f, 0.12f), new Vector2(0.5f, 0.8f));

        Image top = NewImage("REGION_TOP_STAGE2", root, new Color32(0xA8, 0xA8, 0xA8, 255), null);
        Anchor(top.rectTransform, new Vector2(0f, 0.8f), new Vector2(0.5f, 1f));

        Image right = NewImage("REGION_RIGHT_STAGE3", root, new Color32(0xD6, 0xD6, 0xD6, 255), null);
        Anchor(right.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(1f, 1f));

        Text t1 = NewText("TEXT_STAGE1", root, "스테이지 1", 40, dark);
        PlaceAt(t1.rectTransform, new Vector2(0.25f, 0.74f), new Vector2(500f, 60f));
        Text t2 = NewText("TEXT_STAGE2", root, "", 36, dark);
        PlaceAt(t2.rectTransform, new Vector2(0.25f, 0.9f), new Vector2(500f, 60f));
        Text t3 = NewText("TEXT_STAGE3", root, "", 36, dark);
        PlaceAt(t3.rectTransform, new Vector2(0.78f, 0.85f), new Vector2(500f, 60f));
        Text t4 = NewText("TEXT_STAGE4", root, "", 36, dark);
        PlaceAt(t4.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(500f, 60f));

        Image highlight = NewImage("REGION1HIGHLIGHT", root, new Color(1f, 0.85f, 0.2f, 0.18f), null);
        Anchor(highlight.rectTransform, new Vector2(0f, 0.12f), new Vector2(0.5f, 0.8f));
        Outline outline = highlight.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
        outline.effectDistance = new Vector2(8f, 8f);
        outline.useGraphicAlpha = false;

        // ---- 1스테이지 점 + 점선 ----
        RectTransform stage1Map = NewRect("STAGE1MAP", root);
        Stretch(stage1Map);

        RectTransform lineRt = NewRect("LINE", stage1Map);
        Stretch(lineRt);

        RectTransform points = NewRect("POINTS", stage1Map);
        Stretch(points);

        for (int i = 0; i < POINTOFFSETS.Length; i++)
        {
            Image p = NewImage("P" + (i + 1), points, new Color(0.95f, 0.8f, 0.1f, 1f), circle);
            RectTransform prt = p.rectTransform;
            Vector2 a = new Vector2(0.5f + POINTOFFSETS[i].x / REFW, 0.5f + POINTOFFSETS[i].y / REFH);
            prt.anchorMin = a;
            prt.anchorMax = a;
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(24f, 24f);
        }

        UIDASHEDLINE line = lineRt.gameObject.AddComponent<UIDASHEDLINE>();
        line.pointsParent = points;
        line.color = new Color(0.8f, 0.68f, 0.15f, 0.9f);
        line.dashLength = 14f;
        line.gapLength = 10f;
        line.thickness = 4f;

        // ---- 정원 / 커서 / 글자 ----
        Image garden = NewImage("GARDENNODE", root, new Color(0.44f, 0.44f, 0.44f, 1f), circle);
        PlaceAt(garden.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(56f, 56f));

        Image cursor = NewImage("CURSOR", root, new Color(1f, 0.85f, 0.2f, 0.5f), circle);
        PlaceAt(cursor.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(64f, 64f));

        Text label = NewText("LABEL", root, "정원", 40, dark);
        PlaceAt(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(300f, 60f));

        Undo.RegisterCreatedObjectUndo(root.gameObject, "스테이지 맵 생성");

        // ---- 컨트롤러 ----
        GameObject ctrl = new GameObject("STAGEMAP");
        STAGEMAP map = ctrl.AddComponent<STAGEMAP>();
        map.gardenNode = garden.rectTransform;
        map.cursor = cursor.rectTransform;
        map.labelText = label;
        map.equip = Object.FindObjectOfType<EQUIPCONTROLLER>(true);
        map.regions = new MAPREGION[]
        {
            new MAPREGION
            {
                label = "스테이지 1",
                direction = MAPDIR.Left,
                stage = 1,
                highlight = highlight.gameObject,
                pointsRoot = stage1Map.gameObject,
                pointsParent = points
            }
        };
        Undo.RegisterCreatedObjectUndo(ctrl, "STAGEMAP 생성");

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = ctrl;

        if (map.equip == null)
            Debug.LogWarning("[스테이지맵] EQUIPCONTROLLER를 찾지 못했습니다. STAGEMAP의 Equip 칸에 직접 연결하세요.");
        Debug.Log("[스테이지맵] 만들기 완료. Ctrl+S로 저장한 뒤 Play 하세요.");
    }

    // ---------- 도우미 ----------
    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;          // 클릭을 가로채지 않게
        return img;
    }

    static Text NewText(string name, Transform parent, string content, int size, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void PlaceAt(RectTransform rt, Vector2 anchor, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }
}*/