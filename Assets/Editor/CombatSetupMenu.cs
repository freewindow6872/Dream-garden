using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>전투 테스트용 에디터 메뉴. 상단 메뉴 Gardener > Combat 에서 사용.</summary>
public static class CombatSetupMenu
{
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

}
