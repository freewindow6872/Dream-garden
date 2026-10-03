using UnityEngine;

/// <summary>에셋 없이 코드로 만드는 1x1 유닛 흰색 스프라이트. (임시 그래픽/디버그/맵 프리뷰용)</summary>
public static class RuntimeSprites
{
    private static Sprite white;

    public static Sprite White
    {
        get
        {
            if (white == null)
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                Color32[] px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;

                white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                white.hideFlags = HideFlags.HideAndDontSave;
            }
            return white;
        }
    }
}
