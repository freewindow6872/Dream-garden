using UnityEngine;

public static class GAMEDATA
{
    public static int Stage = 1;
    public static int SubStage = 1;

    public static ITEMDATA[] Plants = new ITEMDATA[3];
    public static ITEMDATA Animal;                    

    public static void Select(int stage, int subStage)
    {
        Stage = stage;
        SubStage = subStage;
        PlayerPrefs.SetInt("SelectedStage", stage);
        PlayerPrefs.SetInt("SelectedSubStage", subStage);
        PlayerPrefs.Save();
    }
}