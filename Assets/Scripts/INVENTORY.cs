using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class POTSTATE
{
    public string id;
    public bool planted;
    public bool fertilized;
    public string seedName;        // 심은 씨앗
    public int plantedAtClear;     // 심은 시점의 누적 클리어 횟수
}

[System.Serializable]
public class INVENTORYSAVE
{
    public int gears;
    public int clearCount;                                       // 누적 클리어 횟수
    public List<string> itemNames = new List<string>();          // 쌓이는 아이템 (이름 반복 = 개수)
    public List<string> unlockedNames = new List<string>();      // 해금된 식물/동물
    public List<POTSTATE> pots = new List<POTSTATE>();           // 화분 상태
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

    // ---- 개수 관리 (씨앗, 비료 등) ----
    public static int Count(string itemName)
    {
        int n = 0;
        foreach (string s in Data.itemNames) if (s == itemName) n++;
        return n;
    }

    public static void Add(string itemName, int amount = 1)
    {
        for (int i = 0; i < amount; i++) Data.itemNames.Add(itemName);
        Save();
    }

    public static bool Consume(string itemName, int amount = 1)
    {
        if (Count(itemName) < amount) return false;
        for (int i = 0; i < amount; i++) Data.itemNames.Remove(itemName);
        Save();
        return true;
    }

    // ---- 식물/동물 보유 여부 (장착 목록, 소지품 표시에 사용) ----
    public static bool IsOwned(ITEMDATA item)
    {
        if (item == null) return false;
        return item.startUnlocked
            || Data.unlockedNames.Contains(item.itemName)
            || Data.itemNames.Contains(item.itemName);   // 스테이지에서 주운 경우
    }

    // ---- 클리어 횟수 기록 + 조건 달성한 아이템 해금 ----
    public static List<ITEMDATA> RegisterClear(ITEMDATABASE db)
    {
        Data.clearCount++;
        var newlyUnlocked = new List<ITEMDATA>();

        if (db != null)
        {
            // 1) 클리어 횟수만으로 해금되는 아이템 (Unlock After Clears)
            foreach (ITEMDATA it in db.items)
            {
                if (it == null || it.startUnlocked) continue;
                if (it.unlockAfterClears <= 0) continue;
                if (Data.clearCount < it.unlockAfterClears) continue;
                if (Data.unlockedNames.Contains(it.itemName)) continue;

                Data.unlockedNames.Add(it.itemName);
                newlyUnlocked.Add(it);
            }

            // 2) 화분 수확: 충분히 자랐으면 식물을 얻고 화분을 비움
            foreach (POTSTATE pot in Data.pots)
            {
                if (!pot.planted) continue;

                ITEMDATA seed = db.items.Find(i => i != null && i.itemName == pot.seedName);
                if (seed == null || seed.growsInto == null) continue;
                if (Data.clearCount - pot.plantedAtClear < seed.growAfterClears) continue;

                ITEMDATA plant = seed.growsInto;
                if (!IsOwned(plant))
                {
                    Data.unlockedNames.Add(plant.itemName);
                    newlyUnlocked.Add(plant);
                }

                pot.planted = false;
                pot.fertilized = false;
                pot.seedName = "";
            }
        }

        Save();
        return newlyUnlocked;
    }
    // ---- 화분 ----
    public static POTSTATE GetPot(string id)
    {
        foreach (POTSTATE p in Data.pots) if (p.id == id) return p;
        POTSTATE created = new POTSTATE { id = id };
        Data.pots.Add(created);
        return created;
    }

    // 새 게임 등에서 전체 초기화할 때
    public static void ResetAll()
    {
        data = new INVENTORYSAVE();
        Save();
    }
}