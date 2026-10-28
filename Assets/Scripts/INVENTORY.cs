using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class INVENTORYSAVE
{
    public int gears;
    public List<string> itemNames = new List<string>();
}

public static class INVENTORY
{
    const string KEY = "INVENTORY";
    static INVENTORYSAVE data;

    public static INVENTORYSAVE Data
    {
        get
        {
            if (data == null)
            {
                string json = PlayerPrefs.GetString(KEY, "");
                data = string.IsNullOrEmpty(json) ? new INVENTORYSAVE()
                                                  : JsonUtility.FromJson<INVENTORYSAVE>(json);
            }
            return data;
        }
    }

    public static void Save()
    {
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    // 정산 확인 시: 이번 판 기록을 소지품에 합산
    public static void ApplyRun()
    {
        Data.gears += RUNTRACKER.Gears;
        foreach (ITEMDATA item in RUNTRACKER.Items)
            Data.itemNames.Add(item.itemName);
        Save();
    }
}