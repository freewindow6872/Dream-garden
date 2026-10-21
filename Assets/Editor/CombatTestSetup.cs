#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 전투 테스트용 허수아비 / 총알 봇을 현재 씬에 만들어주는 에디터 메뉴.
///   상단 메뉴  Gardener > Combat Test > ...
///
///  - 그림(스프라이트)은 처음 한 번만 Assets/Art/CombatTest/ 에 임시 도트 PNG로 자동 생성된다.
///    (이미 파일이 있으면 덮어쓰지 않는다 → 같은 이름으로 진짜 그림을 넣거나, 생성된 오브젝트의 Sprite만 교체하면 됨)
///  - 오브젝트는 플레이어 오른쪽 바닥 위에 놓인다. 만든 뒤 자유롭게 옮기면 된다.
///  - 씬 파일에 오브젝트가 저장되므로 팀원과 씬 merge가 걱정되면 프리팹으로 만들어 쓰는 것을 권장.
/// </summary>
public static class CombatTestSetup
{
    private const string ArtDir = "Assets/Art/CombatTest";
    private const int PPU = 16;

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color32 Outline = new Color32(38, 26, 20, 255);

    // ------------------------------------------------------------------ menu

    [MenuItem("Gardener/Combat Test/Add Training Dummy (허수아비)")]
    private static void AddDummyMenu()
    {
        CreateDummy(FindSpawnPoint(4f));
    }

    [MenuItem("Gardener/Combat Test/Add Shooter Bot (총알 봇)")]
    private static void AddBotMenu()
    {
        CreateBot(FindSpawnPoint(9f));
    }

    [MenuItem("Gardener/Combat Test/Add Both (둘 다)")]
    private static void AddBothMenu()
    {
        CreateDummy(FindSpawnPoint(4f));
        CreateBot(FindSpawnPoint(9f));
    }

    // ------------------------------------------------------------------ create

    private static GameObject CreateDummy(Vector3 feetPos)
    {
        GameObject fromPrefab = InstantiateFromPrefab("TrainingDummy", feetPos);
        if (fromPrefab != null) return fromPrefab;

        Sprite sprite = GetOrCreateSprite("Scarecrow", BuildScarecrowTexture(), 1f / 34f);

        GameObject root = new GameObject("TrainingDummy");
        Undo.RegisterCreatedObjectUndo(root, "Add Training Dummy");
        root.transform.position = feetPos;

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.95f, 1.9f);
        box.offset = new Vector2(0f, 0.95f);

        Health health = root.AddComponent<Health>();
        SetInt(health, "maxHealth", 200);          // 팀은 기본값이 Enemy

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;

        root.AddComponent<DamagePopupEmitter>();
        root.AddComponent<EnemyHealthBar>();
        TrainingDummy dummy = root.AddComponent<TrainingDummy>();
        SetObject(dummy, "visual", visual.transform);

        SetEnemyLayer(root);
        Finish(root);
        return root;
    }

    private static GameObject CreateBot(Vector3 feetPos)
    {
        GameObject fromPrefab = InstantiateFromPrefab("ShooterBot", feetPos);
        if (fromPrefab != null) return fromPrefab;

        Sprite sprite = GetOrCreateSprite("ShooterBot", BuildBotTexture(), 1f / 18f);

        GameObject root = new GameObject("ShooterBot");
        Undo.RegisterCreatedObjectUndo(root, "Add Shooter Bot");
        root.transform.position = feetPos;

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.95f, 0.9f);
        box.offset = new Vector2(0f, 0.45f);

        Health health = root.AddComponent<Health>();
        SetInt(health, "maxHealth", 40);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;
        sr.flipX = true;                           // 기본: 왼쪽(플레이어 쪽)을 보고 쏨

        root.AddComponent<HitReaction>();
        root.AddComponent<EnemyDeath>();
        root.AddComponent<DamagePopupEmitter>();
        root.AddComponent<EnemyHealthBar>();
        root.AddComponent<ShooterBot>();           // facingRight 기본값 false = 왼쪽

        SetEnemyLayer(root);
        Finish(root);
        return root;
    }

    /// <summary>Assets/Prefabs/Combat/ 에 프리팹이 있으면 그걸 현재 씬에 배치 (프리팹 연결 유지)</summary>
    private static GameObject InstantiateFromPrefab(string prefabName, Vector3 pos)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/" + prefabName + ".prefab");
        if (prefab == null) return null;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Undo.RegisterCreatedObjectUndo(go, "Add " + prefabName);
        go.transform.position = pos;
        Finish(go);
        return go;
    }

    /// <summary>프로젝트 설정에 등록된 "Enemy" 레이어를 적용 (없으면 그대로 둠)</summary>
    private static void SetEnemyLayer(GameObject go)
    {
        int layer = LayerMask.NameToLayer(CombatPhysicsSetup.EnemyLayerName);
        if (layer < 0) return;
        Transform[] all = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
    }

    private static void Finish(GameObject go)
    {
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // ------------------------------------------------------------------ placement

    /// <summary>플레이어 기준 오른쪽 dx 위치의 바닥(Ground 레이어) 위. 바닥을 못 찾으면 플레이어 발 높이.</summary>
    private static Vector3 FindSpawnPoint(float dx)
    {
        Vector2 origin = Vector2.zero;
        float fallbackY = 0f;

        PlayerCombat player = Object.FindObjectOfType<PlayerCombat>();
        if (player != null)
        {
            origin = (Vector2)player.transform.position + Vector2.right * dx;
            Collider2D pc = player.GetComponent<Collider2D>();
            fallbackY = pc != null ? pc.bounds.min.y : player.transform.position.y - 0.5f;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;
            origin = new Vector2(pivot.x, pivot.y);
            fallbackY = pivot.y;
        }

        Physics2D.SyncTransforms();
        RaycastHit2D hit = Physics2D.Raycast(origin + Vector2.up * 5f, Vector2.down, 40f, GameLayers.GroundMaskValue);
        float y = hit.collider != null ? hit.point.y : fallbackY;
        return new Vector3(origin.x, y, 0f);
    }

    // ------------------------------------------------------------------ serialized helpers

    private static void SetInt(Object target, string field, int value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ sprite generation

    private static Sprite GetOrCreateSprite(string fileName, Texture2D tex, float pivotY)
    {
        Directory.CreateDirectory(ArtDir);
        string path = ArtDir + "/" + fileName + ".png";

        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PPU;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, pivotY);   // 발 밑 중앙
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        Object.DestroyImmediate(tex);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 도트 그리기 도우미: 그림 영역 좌표(좌하단 0,0)에 1픽셀 여백(외곽선 자리)을 더해서 칠한다.
    private static void R(Color32[] px, int texW, int x0, int y0, int x1, int y1, Color c)
    {
        Color32 col = c;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                px[(y + 1) * texW + (x + 1)] = col;
    }

    private static Texture2D Finalize(Color32[] px, int w, int h)
    {
        // 투명 픽셀 중 상하좌우에 그림이 붙어 있는 곳을 어두운 외곽선으로 채움
        Color32[] outlined = (Color32[])px.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a != 0) continue;
                bool near =
                    (x > 0 && px[y * w + x - 1].a != 0) ||
                    (x < w - 1 && px[y * w + x + 1].a != 0) ||
                    (y > 0 && px[(y - 1) * w + x].a != 0) ||
                    (y < h - 1 && px[(y + 1) * w + x].a != 0);
                if (near) outlined[y * w + x] = Outline;
            }
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(outlined);
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildScarecrowTexture()
    {
        const int W = 22, H = 34;   // 그림 20x32 + 외곽선 여백
        Color32[] px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = Clear;

        Color wood = new Color(0.50f, 0.32f, 0.15f);
        Color straw = new Color(0.93f, 0.80f, 0.30f);
        Color shirt = new Color(0.35f, 0.50f, 0.75f);
        Color shirtDark = new Color(0.27f, 0.40f, 0.62f);
        Color patch = new Color(0.85f, 0.70f, 0.35f);
        Color sack = new Color(0.88f, 0.75f, 0.50f);
        Color dark = new Color(0.15f, 0.10f, 0.08f);
        Color hat = new Color(0.40f, 0.25f, 0.12f);
        Color band = new Color(0.80f, 0.20f, 0.20f);

        R(px, W, 9, 0, 10, 21, wood);          // 기둥
        R(px, W, 1, 14, 18, 16, wood);         // 팔 막대
        R(px, W, 0, 12, 2, 17, straw);         // 왼손 지푸라기
        R(px, W, 17, 12, 19, 17, straw);       // 오른손 지푸라기
        R(px, W, 5, 8, 14, 19, shirt);         // 옷
        R(px, W, 5, 8, 14, 9, shirtDark);      // 옷 아랫단
        R(px, W, 7, 12, 9, 14, patch);         // 기운 천
        R(px, W, 5, 5, 6, 7, straw);           // 옷 아래로 삐져나온 지푸라기
        R(px, W, 8, 4, 9, 7, straw);
        R(px, W, 11, 5, 12, 7, straw);
        R(px, W, 13, 6, 14, 7, straw);
        R(px, W, 6, 21, 13, 28, sack);         // 얼굴(자루)
        R(px, W, 8, 25, 8, 25, dark);          // 눈
        R(px, W, 11, 25, 11, 25, dark);
        R(px, W, 8, 23, 8, 23, dark);          // 꿰맨 입
        R(px, W, 10, 23, 10, 23, dark);
        R(px, W, 4, 28, 15, 29, hat);          // 모자 챙
        R(px, W, 7, 30, 12, 31, hat);          // 모자 윗부분
        R(px, W, 7, 30, 12, 30, band);         // 모자 띠

        return Finalize(px, W, H);
    }

    private static Texture2D BuildBotTexture()
    {
        const int W = 20, H = 18;   // 그림 18x16 + 외곽선 여백 (오른쪽을 보는 기준)
        Color32[] px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = Clear;

        Color steel = new Color(0.55f, 0.62f, 0.70f);
        Color steelDark = new Color(0.35f, 0.40f, 0.48f);
        Color steelLight = new Color(0.75f, 0.80f, 0.86f);
        Color gun = new Color(0.25f, 0.27f, 0.30f);
        Color dark = new Color(0.12f, 0.12f, 0.15f);
        Color red = new Color(0.95f, 0.20f, 0.20f);

        R(px, W, 1, 0, 14, 2, gun);            // 바퀴/캐터필러
        R(px, W, 3, 1, 4, 1, steel);
        R(px, W, 7, 1, 8, 1, steel);
        R(px, W, 11, 1, 12, 1, steel);
        R(px, W, 2, 3, 13, 12, steel);         // 몸통
        R(px, W, 2, 12, 13, 12, steelLight);   // 윗면 하이라이트
        R(px, W, 2, 3, 13, 4, steelDark);      // 아랫면 그늘
        R(px, W, 3, 6, 6, 9, steelDark);       // 패널
        R(px, W, 4, 7, 4, 7, steelLight);
        R(px, W, 8, 8, 12, 11, dark);          // 바이저
        R(px, W, 11, 9, 12, 10, red);          // 빨간 눈
        R(px, W, 6, 13, 6, 14, gun);           // 안테나
        R(px, W, 6, 15, 6, 15, red);
        R(px, W, 14, 5, 17, 7, gun);           // 총신 (오른쪽)
        R(px, W, 17, 5, 17, 7, dark);          // 총구
        R(px, W, 14, 8, 15, 8, steelDark);     // 총신 받침

        return Finalize(px, W, H);
    }
}
#endif
