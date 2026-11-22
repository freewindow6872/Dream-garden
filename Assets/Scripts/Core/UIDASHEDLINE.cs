using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIDASHEDLINE : MaskableGraphic
{
    [Tooltip("점들이 자식으로 들어 있는 부모. 자식 순서대로 점선으로 이어집니다.")]
    public Transform pointsParent;

    public float dashLength = 14f;
    public float gapLength = 10f;
    public float thickness = 3f;

    int lastHash;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;      // 클릭을 가로채지 않게
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif

    // 점을 옮기면 점선도 다시 그림 (편집 중에도 동작)
    void LateUpdate()
    {
        int h = ComputeHash();
        if (h != lastHash)
        {
            lastHash = h;
            SetVerticesDirty();
        }
    }

    int ComputeHash()
    {
        if (pointsParent == null) return 0;
        unchecked
        {
            int h = pointsParent.childCount;
            for (int i = 0; i < pointsParent.childCount; i++)
                h = h * 31 + pointsParent.GetChild(i).position.GetHashCode();
            h = h * 31 + transform.position.GetHashCode();
            return h;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (pointsParent == null) return;

        for (int i = 0; i < pointsParent.childCount - 1; i++)
        {
            Vector2 a = transform.InverseTransformPoint(pointsParent.GetChild(i).position);
            Vector2 b = transform.InverseTransformPoint(pointsParent.GetChild(i + 1).position);
            AddDashes(vh, a, b);
        }
    }

    void AddDashes(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.01f) return;
        dir /= len;

        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
        float step = Mathf.Max(0.1f, dashLength + gapLength);

        for (float s = 0f; s < len; s += step)
        {
            float e = Mathf.Min(s + dashLength, len);
            Vector2 p0 = a + dir * s;
            Vector2 p1 = a + dir * e;

            int idx = vh.currentVertCount;
            vh.AddVert(p0 - n, color, Vector2.zero);
            vh.AddVert(p0 + n, color, Vector2.zero);
            vh.AddVert(p1 + n, color, Vector2.zero);
            vh.AddVert(p1 - n, color, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx);
        }
    }
}