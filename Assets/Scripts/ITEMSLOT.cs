using UnityEngine;
using UnityEngine.UI;

public class ITEMSLOT : MonoBehaviour
{
    public ITEMTYPE type;
    public int index;          // 식물 0~2, 동물 0
    public Image iconImage;

    EQUIPCONTROLLER controller;

    void Awake()
    {
        controller = FindObjectOfType<EQUIPCONTROLLER>(true);
        GetComponent<Button>().onClick.AddListener(() => controller.OpenList(this));
    }

    public void SetItem(ITEMDATA item)
    {
        if (item != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }
    }
}