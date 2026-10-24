using UnityEngine;

public enum ITEMTYPE { Plant, Animal }

[CreateAssetMenu(fileName = "NEWITEM", menuName = "Game/Item")]
public class ITEMDATA : ScriptableObject
{
    public string itemName;
    public ITEMTYPE type;
    public Sprite icon;
    [TextArea] public string description;
}