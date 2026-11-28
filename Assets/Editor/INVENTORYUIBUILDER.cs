using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class INVENTORYUIBUILDER
{
    const string PATH = "Assets/Resources/INVENTORYUI.prefab";

    static readonly Color32 COLBG = new Color32(0xF0, 0xF0, 0xE6, 255);
    static readonly Color32 COLSOFT = new Color32(0xE0, 0xE2, 0xD5, 255);
    static readonly Color32 COLOLIVE = new Color32(0x6B, 0x76, 0x55, 255);
    static readonly Color32 COLTEXT = new Color32(0x4A, 0x55, 0x3A, 255);
    static readonly Color32 COLSLOT = new Color32(0xFA, 0xFA, 0xF5, 255);
    static readonly Vector2 CENTER = new Vector2(0.5f, 0.5f);

    static Font font;

    [MenuItem("Tools/소지품 UI 자동 만들기")]
    static void Build()
    {
        if (!EditorUtility.DisplayDialog("소지품 UI",
            "Assets/Resources/INVENTORYUI 프리팹을 시안 구성으로 새로 만듭니다.\n이미 있으면 덮어쓰니, 직접 고친 내용이 있으면 사라집니다. 계속할까요?",
            "만들기", "취소"))
            return;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ---- 루트 (Canvas) ----
        GameObject rootGo = new GameObject("INVENTORYUI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        INVENTORYUI ui = rootGo.AddComponent<INVENTORYUI>();

        // ---- 배경 패널 ----
        Image panel = Img("INVENTORYPANEL", rootGo.transform, COLBG, true);
        Stretch(panel.rectTransform, 0f);
        Transform p = panel.transform;

        // ---- 뒤로 버튼 / 톱니바퀴 개수 ----
        Button back = Btn("BACKBUTTON", p, "<", COLSOFT, 48);
        Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(70f, -70f), new Vector2(80f, 80f));

        Text gear = Txt("GEARTEXT", p, "톱니바퀴  0", 34, TextAnchor.MiddleRight, COLTEXT);
        Place(gear.rectTransform, new Vector2(1f, 1f), new Vector2(-230f, -70f), new Vector2(400f, 50f));

        // ---- 왼쪽: 캐릭터 영역 (자리만 잡아둠) ----
        Image character = Img("CHARACTERPANEL", p, COLSOFT, false);
        Place(character.rectTransform, CENTER, new Vector2(-640f, -20f), new Vector2(520f, 720f));
        Text charText = Txt("TEXT", character.transform, "캐릭터", 40, TextAnchor.MiddleCenter, COLOLIVE);
        Stretch(charText.rectTransform, 0f);

        // ---- 가운데 위: 탭 4개 ----
        string[] tabNames = { "식물", "씨앗", "나뭇잎", "톱니" };
        ITEMTYPE[][] tabTypes =
        {
            new[] {  ITEMTYPE.Plant },
            new[] { ITEMTYPE.Seed },
            new[] { ITEMTYPE.Animal },
            new[] { ITEMTYPE.Consumable }
        };

        ui.tabs = new INVENTORYTAB[tabNames.Length];
        for (int i = 0; i < tabNames.Length; i++)
        {
            Button b = Btn("TAB" + (i + 1), p, tabNames[i], COLSOFT, 30);
            Place(b.GetComponent<RectTransform>(), CENTER, new Vector2(-215f + 170f * i, 310f), new Vector2(150f, 64f));

            ui.tabs[i] = new INVENTORYTAB
            {
                name = tabNames[i],
                button = b,
                background = b.GetComponent<Image>(),
                types = tabTypes[i]
            };
        }

        // ---- 가운데: 4열 아이템 칸 (스크롤) ----
        Image scrollBg = Img("SCROLL", p, new Color(1f, 1f, 1f, 0.35f), true);
        Place(scrollBg.rectTransform, CENTER, new Vector2(40f, -60f), new Vector2(556f, 556f));
        ScrollRect scroll = scrollBg.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = NewRect("VIEWPORT", scrollBg.transform);
        Stretch(viewport, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewRect("CONTENT", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(130f, 130f);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // ---- 칸 견본 (비활성) ----
        Image slot = Img("SLOTTEMPLATE", p, COLSLOT, true);
        Button slotBtn = slot.gameObject.AddComponent<Button>();
        slotBtn.targetGraphic = slot;

        Image icon = Img("ICON", slot.transform, Color.white, false);
        Stretch(icon.rectTransform, 14f);
        icon.preserveAspect = true;

        Image badge = Img("BADGE", slot.transform, COLOLIVE, false);
        RectTransform badgeRt = badge.rectTransform;
        badgeRt.anchorMin = new Vector2(1f, 0f);
        badgeRt.anchorMax = new Vector2(1f, 0f);
        badgeRt.pivot = new Vector2(1f, 0f);
        badgeRt.anchoredPosition = new Vector2(-8f, 8f);
        badgeRt.sizeDelta = new Vector2(60f, 28f);
        Text badgeText = Txt("TEXT", badge.transform, "x1", 20, TextAnchor.MiddleCenter, Color.white);
        Stretch(badgeText.rectTransform, 0f);

        slot.gameObject.SetActive(false);

        // ---- 오른쪽: 상세 정보 ----
        RectTransform detail = NewRect("DETAIL", p);
        Place(detail, CENTER, new Vector2(620f, 0f), new Vector2(520f, 760f));

        Image frame = Img("FRAME", detail, COLSOFT, false);
        Place(frame.rectTransform, CENTER, new Vector2(0f, 170f), new Vector2(400f, 400f));

        Image detailImg = Img("DETAILIMAGE", frame.transform, Color.white, false);
        Stretch(detailImg.rectTransform, 24f);
        detailImg.preserveAspect = true;

        Text detailName = Txt("NAME", detail, "아이템 이름", 44, TextAnchor.MiddleLeft, COLTEXT);
        Place(detailName.rectTransform, CENTER, new Vector2(-70f, -100f), new Vector2(340f, 60f));

        Image countBadge = Img("COUNTBADGE", detail, COLOLIVE, false);
        Place(countBadge.rectTransform, CENTER, new Vector2(180f, -100f), new Vector2(90f, 44f));
        Text countText = Txt("TEXT", countBadge.transform, "x1", 28, TextAnchor.MiddleCenter, Color.white);
        Stretch(countText.rectTransform, 0f);

        Image divider = Img("DIVIDER", detail, COLOLIVE, false);
        Place(divider.rectTransform, CENTER, new Vector2(0f, -150f), new Vector2(480f, 3f));

        Text desc = Txt("DESCRIPTION", detail, "설명", 30, TextAnchor.UpperLeft, COLTEXT);
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        Place(desc.rectTransform, CENTER, new Vector2(0f, -290f), new Vector2(480f, 240f));

        // ---- INVENTORYUI 연결 ----
        ui.panel = panel.gameObject;
        ui.closeButton = back;
        ui.gearText = gear;
        ui.scroll = scroll;
        ui.slotContainer = content;
        ui.slotTemplate = slot.gameObject;
        ui.minSlots = 16;
        ui.detailRoot = detail.gameObject;
        ui.detailImage = detailImg;
        ui.detailName = detailName;
        ui.detailCount = countText;
        ui.detailCountBadge = countBadge.gameObject;
        ui.detailDescription = desc;
        ui.database = FindDatabase();

        if (ui.database == null)
            Debug.LogWarning("[소지품 UI] ITEMDATABASE 에셋을 찾지 못했습니다. 프리팹의 Database 칸에 직접 연결하세요.");

        // ---- 프리팹으로 저장 ----
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        PrefabUtility.SaveAsPrefabAsset(rootGo, PATH);
        Object.DestroyImmediate(rootGo);
        AssetDatabase.SaveAssets();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
        Debug.Log("[소지품 UI] " + PATH + " 생성 완료. Play 후 I키로 확인하세요.");
    }

    static ITEMDATABASE FindDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:ITEMDATABASE");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<ITEMDATABASE>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : null;
    }

    // ---------- 도우미 ----------
    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image Img(string name, Transform parent, Color color, bool raycast)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static Text Txt(string name, Transform parent, string content, int size, TextAnchor align, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button Btn(string name, Transform parent, string label, Color bg, int size)
    {
        Image img = Img(name, parent, bg, true);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        Text t = Txt("TEXT", img.transform, label, size, TextAnchor.MiddleCenter, COLTEXT);
        Stretch(t.rectTransform, 0f);
        return b;
    }

    static void Stretch(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}