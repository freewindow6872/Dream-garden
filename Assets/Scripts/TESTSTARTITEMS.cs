using UnityEngine;

public class TESTSTARTITEMS : MonoBehaviour
{
    [System.Serializable]
    public class STARTITEM
    {
        public ITEMDATA item;
        public int amount = 5;
    }

    [Tooltip("체크하면 정원에 들어올 때마다 부족한 만큼 다시 채움. 끄면 처음 한 번만 지급")]
    public bool refillEveryTime = true;

    public STARTITEM[] items;

    const string DONEKEY = "TESTSTARTITEMS_DONE";

    void Start()
    {
        if (!refillEveryTime && PlayerPrefs.GetInt(DONEKEY, 0) == 1) return;

        foreach (STARTITEM s in items)
        {
            if (s.item == null) continue;
            int have = INVENTORY.Count(s.item.itemName);
            if (have < s.amount)
                INVENTORY.Add(s.item.itemName, s.amount - have);   // 부족한 만큼만 추가
        }

        PlayerPrefs.SetInt(DONEKEY, 1);
        PlayerPrefs.Save();
    }
}