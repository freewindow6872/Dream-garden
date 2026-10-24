using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ITEMDATABASE", menuName = "Game/Item Database")]
public class ITEMDATABASE : ScriptableObject
{
    public List<ITEMDATA> items = new List<ITEMDATA>();

    public List<ITEMDATA> GetByType(ITEMTYPE type)
    {
        return items.FindAll(i => i.type == type);
    }
}