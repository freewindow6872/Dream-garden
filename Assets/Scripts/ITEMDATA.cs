using UnityEngine;

public enum ITEMTYPE { Plant, Animal, Seed, Consumable }

[CreateAssetMenu(fileName = "NEWITEM", menuName = "Game/Item")]
public class ITEMDATA : ScriptableObject
{
    public string itemName;
    public ITEMTYPE type;
    public Sprite icon;
    [TextArea] public string description;

    [Header("식물/동물 획득 조건")]
    [Tooltip("체크하면 처음부터 사용 가능")]
    public bool startUnlocked = true;

    [Tooltip("0이면 클리어로 해금 안 됨. N이면 누적 클리어 N회째에 해금 (Start Unlocked를 끄고 사용)")]
    [Min(0)] public int unlockAfterClears = 0;
    [Header("씨앗 전용 (Type이 Seed일 때)")]
    [Tooltip("이 씨앗이 자라서 되는 식물")]
    public ITEMDATA growsInto;

    [Tooltip("심은 뒤 이만큼 스테이지를 클리어하면 식물이 되고 화분이 비워짐")]
    [Min(1)] public int growAfterClears = 2;
}