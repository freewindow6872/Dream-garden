using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum MAPDIR { Left, Up, Right, Down }

// 정원 사방의 스테이지 영역 하나
[System.Serializable]
public class MAPREGION
{
    public string label = "스테이지 1";
    public MAPDIR direction = MAPDIR.Left;
    public int stage = 1;

    [Tooltip("영역을 지정했을 때 켜지는 테두리/강조 오브젝트")]
    public GameObject highlight;

    [Tooltip("점과 점선을 묶은 오브젝트. 영역을 확정했을 때만 보임")]
    public GameObject pointsRoot;

    [Tooltip("점들이 자식으로 들어 있는 부모. 자식 순서대로 1-1, 1-2 ... (정원에 가까운 점이 첫 번째)")]
    public Transform pointsParent;

    [System.NonSerialized] public List<RectTransform> points = new List<RectTransform>();

    public void BuildPoints()
    {
        points.Clear();
        if (pointsParent == null) return;
        foreach (Transform c in pointsParent)
        {
            RectTransform rt = c as RectTransform;
            if (rt != null) points.Add(rt);
        }
    }
}

public class STAGEMAP : MonoBehaviour
{
    enum STATE { Garden, Region, Points }

    [Header("정원 / 커서")]
    public RectTransform gardenNode;             // 가운데 정원 위치
    public RectTransform cursor;                 // 이동하는 커서 (지정 표시)
    public Text labelText;                       // "1-1" 등이 표시될 글자
    public string gardenLabel = "정원";
    public Vector2 labelOffset = new Vector2(0f, 70f);
    public float cursorSpeed = 14f;

    [Header("스테이지 영역 (정원 사방)")]
    public MAPREGION[] regions;

    [Header("연결")]
    public EQUIPCONTROLLER equip;
    public string gardenSceneName = "GARDEN";

    STATE state = STATE.Garden;
    MAPREGION region;
    int pointIndex;
    bool equipWasOpen;

    void Start()
    {
        foreach (MAPREGION r in regions)
        {
            r.BuildPoints();
            if (r.highlight != null) r.highlight.SetActive(false);
            if (r.pointsRoot != null) r.pointsRoot.SetActive(false);
        }

        state = STATE.Garden;
        SetLabel(gardenLabel);

        if (cursor != null && gardenNode != null) cursor.position = gardenNode.position;
    }

    void Update()
    {
        MoveCursor();

        // 장착 화면이 열려 있거나 방금 닫힌 프레임에는 입력을 무시
        bool equipOpen = equip != null && equip.IsOpen;
        bool block = equipOpen || equipWasOpen;
        equipWasOpen = equipOpen;
        if (block) return;

        Vector2 arrow = ReadArrow();
        bool cancel = MAPINPUT.Cancel;
        bool confirm = MAPINPUT.ConfirmKey || (MAPINPUT.Click && !PointerOverUI());

        switch (state)
        {
            case STATE.Garden:
                if (arrow != Vector2.zero)
                {
                    MAPREGION r = FindRegion(arrow);
                    if (r != null) SelectRegion(r);
                }
                else if (cancel)
                {
                    SceneManager.LoadScene(gardenSceneName);
                }
                break;

            case STATE.Region:
                if (cancel || (arrow != Vector2.zero && ToDir(-arrow) == region.direction))
                {
                    BackToGarden();                       // 반대 방향 키 또는 Esc
                }
                else if (arrow != Vector2.zero)
                {
                    MAPREGION r = FindRegion(arrow);      // 다른 영역으로 전환
                    if (r != null) SelectRegion(r);
                }
                else if (confirm)
                {
                    EnterPoints();
                }
                break;

            case STATE.Points:
                if (cancel) LeavePoints();
                else if (arrow != Vector2.zero) MovePoint(arrow);
                else if (confirm) ChooseStage();
                break;
        }
    }

    // ---------- 상태 전환 ----------
    void SelectRegion(MAPREGION r)
    {
        if (region != null && region.highlight != null) region.highlight.SetActive(false);

        region = r;
        state = STATE.Region;
        if (region.highlight != null) region.highlight.SetActive(true);
        SetLabel(region.label);
    }

    void BackToGarden()
    {
        if (region != null && region.highlight != null) region.highlight.SetActive(false);
        region = null;
        state = STATE.Garden;
        SetLabel(gardenLabel);
    }

    void EnterPoints()
    {
        if (region.points.Count == 0)
        {
            Debug.LogWarning("[스테이지맵] " + region.label + "에 점이 없습니다. Points Parent의 자식을 확인하세요.");
            return;
        }

        state = STATE.Points;
        if (region.pointsRoot != null) region.pointsRoot.SetActive(true);
        SelectPoint(0);
    }

    void LeavePoints()
    {
        if (region.pointsRoot != null) region.pointsRoot.SetActive(false);
        state = STATE.Region;
        SetLabel(region.label);
    }

    // ---------- 점 선택 ----------
    void SelectPoint(int index)
    {
        pointIndex = index;
        SetLabel(region.stage + "-" + (index + 1));
    }

    /// 왼쪽 키: 다음 점(+1), 오른쪽 키: 이전 점(-1)으로 이동
    void MovePoint(Vector2 arrow)
    {
        List<RectTransform> pts = region.points;
        int cand = pointIndex;

        if (arrow.x < 0f) cand = pointIndex + 1;      // 왼쪽 화살표 -> 다음 스테이지 점으로
        else if (arrow.x > 0f) cand = pointIndex - 1; // 오른쪽 화살표 -> 이전 스테이지 점으로

        if (cand >= 0 && cand < pts.Count)
        {
            SelectPoint(cand);
        }
    }

    void ChooseStage()
    {
        GAMEDATA.Select(region.stage, pointIndex + 1);

        if (equip != null) equip.OpenEquipPanel();
        else Debug.LogWarning("[스테이지맵] Equip(EQUIPCONTROLLER)이 연결되지 않았습니다.");
    }

    // ---------- 보조 ----------
    Vector2 ReadArrow()
    {
        if (MAPINPUT.Left) return Vector2.left;
        if (MAPINPUT.Right) return Vector2.right;

        return Vector2.zero;
    }

    MAPDIR ToDir(Vector2 a)
    {
        if (a.x < 0f) return MAPDIR.Left;
        if (a.x > 0f) return MAPDIR.Right;
        if (a.y > 0f) return MAPDIR.Up;
        return MAPDIR.Down;
    }

    MAPREGION FindRegion(Vector2 arrow)
    {
        MAPDIR d = ToDir(arrow);
        foreach (MAPREGION r in regions)
            if (r.direction == d) return r;
        return null;
    }

    bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void SetLabel(string text)
    {
        if (labelText != null) labelText.text = text;
    }

    void MoveCursor()
    {
        if (cursor == null) return;

        Transform target = gardenNode;
        if (state == STATE.Points && region != null && pointIndex < region.points.Count)
            target = region.points[pointIndex];
        if (target == null) return;

        float k = 1f - Mathf.Exp(-cursorSpeed * Time.unscaledDeltaTime);
        cursor.position = Vector3.Lerp(cursor.position, target.position, k);

        if (labelText != null)
            labelText.rectTransform.position = cursor.position + (Vector3)labelOffset;
    }
}