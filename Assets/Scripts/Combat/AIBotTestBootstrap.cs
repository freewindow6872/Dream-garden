using UnityEngine;
using UnityEngine.SceneManagement;

public class AIBotTestBootstrap : MonoBehaviour
{
    void Start() {
        GameObject old = GameObject.Find("ShooterBot"); if (old) Destroy(old);
        old = GameObject.Find("TrainingDummy"); if (old) Destroy(old);
        GameObject player = GameObject.Find("player");
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        // A broad, clearly visible test floor and two raised platforms.
        MakePlatform("AI Test Floor", new Vector2(0,-1.2f), new Vector2(30,1), new Color(.22f,.25f,.3f));
        MakePlatform("AI Test Platform Left", new Vector2(-7,2), new Vector2(6,.5f), new Color(.28f,.32f,.36f));
        MakePlatform("AI Test Platform Right", new Vector2(7,3), new Vector2(6,.5f), new Color(.28f,.32f,.36f));
        if (player) { player.transform.position = new Vector3(-10,0,0); player.tag = "Player"; }
        CreateBot("Scout Bot", TestBotAI.BotType.Scout, new Vector2(-4,0), new Color(.2f,.8f,.3f), player ? player.transform : null, -5, 0);
        CreateBot("Sniper Bot", TestBotAI.BotType.Sniper, new Vector2(2,0), new Color(.95f,.25f,.2f), player ? player.transform : null, 0, 4);
        CreateBot("Drone Bot", TestBotAI.BotType.Drone, new Vector2(8,4), new Color(.2f,.65f,1f), player ? player.transform : null, 5, 11);
        if (Camera.main) { var cam = Camera.main.GetComponent<CameraFollow2D>(); if (cam && player) { /* existing camera follow remains */ } }
    }
    void MakePlatform(string n, Vector2 pos, Vector2 size, Color color) {
        var go = new GameObject(n); go.transform.position = pos; go.transform.localScale = new Vector3(size.x,size.y,1);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = RuntimeSprites.White; sr.color = color; sr.sortingOrder = -1;
        var box = go.AddComponent<BoxCollider2D>(); box.size = Vector2.one;
    }
    void CreateBot(string n, TestBotAI.BotType type, Vector2 pos, Color color, Transform player, float minX, float maxX) {
        var go = new GameObject(n); go.transform.position = pos; go.transform.localScale = new Vector3(.8f,.8f,1);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = RuntimeSprites.White; sr.color = color; sr.sortingOrder = 2;
        var box = go.AddComponent<BoxCollider2D>(); box.size = Vector2.one;
        var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.bodyType = RigidbodyType2D.Kinematic;
        var ai = go.AddComponent<TestBotAI>(); ai.type = type; ai.player = player; ai.patrolMinX = minX; ai.patrolMaxX = maxX;
        go.AddComponent<Health>().Configure(Team.Enemy,3,0);
    }
}
