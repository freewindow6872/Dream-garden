using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BOTLAYOUT", menuName = "Game/Bot Layout")]
public class BOTLAYOUT : ScriptableObject
{
    public int stage = 1;
    public int subStage = 1;
    public List<ENTRY> entries = new List<ENTRY>();

    [System.Serializable]
    public struct ENTRY
    {
        public GameObject prefab;
        public Vector3 position;
        public float rotationZ;
        public Vector3 scale;
    }
}