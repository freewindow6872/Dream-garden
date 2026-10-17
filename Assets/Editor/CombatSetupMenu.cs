using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>전투 테스트용 에디터 메뉴. 상단 메뉴 Gardener > Combat 에서 사용.</summary>
public static class CombatSetupMenu
{
    private const string WhitePath = "Assets/Placeholders/white.png";

    [MenuItem("Gardener/Combat/Create Test Robot")]
    private static void CreateTestRobot()
    {
        GameObject go = new GameObject("TestRobot");
        Undo.RegisterCreatedObjectUndo(go, "Create Test Robot");

        SceneView sv = SceneView.lastActiveSceneView;
        Vector3 pos = sv != null ? sv.pivot : Vector3.zero;
        pos.z = 0f;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(1.2f, 1.6f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = EnsureWhiteSprite();
        sr.color = new Color(0.55f, 0.62f, 0.75f, 1f);

        go.AddComponent<BoxCollider2D>().size = Vector2.one;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Health health = go.AddComponent<Health>();
        health.Configure(Team.Enemy, 30, 0f);
        EditorUtility.SetDirty(health);

        go.AddComponent<HitReaction>();
        go.AddComponent<EnemyDeath>();

        // AI 없이 피격/체력 UI를 시험할 수 있게, 2초마다 자동으로 휘두르도록 설정
        EnemyAttackBox box = go.AddComponent<EnemyAttackBox>();
        SerializedObject so = new SerializedObject(box);
        so.FindProperty("testStrikeInterval").floatValue = 2f;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = go;
    }

    [MenuItem("Gardener/Combat/Create Weapon Data Asset")]
    private static void CreateWeaponAsset()
    {
        Directory.CreateDirectory("Assets/Data");
        WeaponData w = ScriptableObject.CreateInstance<WeaponData>();
        w.ApplyOldScissorsDefaults();

        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Data/Weapon_OldScissors.asset");
        AssetDatabase.CreateAsset(w, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = w;
        EditorGUIUtility.PingObject(w);
        Debug.Log("무기 데이터 생성: " + path + "  → 플레이어의 PlayerCombat > Weapon 칸에 넣으면 선딜/후딜/판정 크기를 인스펙터에서 수정할 수 있어요.");
    }

    private static Sprite EnsureWhiteSprite()
    {
        if (!File.Exists(WhitePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WhitePath));

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(WhitePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(WhitePath);
            TextureImporter ti = AssetImporter.GetAtPath(WhitePath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spritePixelsPerUnit = 4f;
                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
    }
}
