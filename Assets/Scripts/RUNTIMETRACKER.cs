using System.Collections.Generic;
using UnityEngine;

public static class RUNTRACKER
{
    public static float PlayTime;
    public static int Gears;                                   // ≈È¥œπŸƒ˚
    public static List<ITEMDATA> Items = new List<ITEMDATA>(); // »πµÊ«— æ∆¿Ã≈€

    static float startTime;
    static bool running;

    public static void StartRun()
    {
        PlayTime = 0f;
        Gears = 0;
        Items.Clear();
        startTime = Time.time;
        running = true;
    }

    public static void AddGear(int amount = 1) { Gears += amount; }

    public static void AddItem(ITEMDATA item)
    {
        if (item != null) Items.Add(item);
    }

    public static void EndRun()
    {
        if (!running) return;
        PlayTime = Time.time - startTime;
        running = false;
    }

    public static string FormatTime()
    {
        int total = Mathf.FloorToInt(PlayTime);
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}