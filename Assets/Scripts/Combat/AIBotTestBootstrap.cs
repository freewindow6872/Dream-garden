using UnityEngine;

public class AIBotTestBootstrap : MonoBehaviour
{
    [Header("Bot spawn positions")]
    [SerializeField] private Vector2 scoutSpawn = new Vector2(-4f, 0f);
    [SerializeField] private Vector2 sniperSpawn = new Vector2(2f, 0f);
    [SerializeField] private Vector2 droneSpawn = new Vector2(8f, 3f);
    [Header("Patrol limits")]
    [SerializeField] private Vector2 scoutPatrolX = new Vector2(-6f, 0f);
    [SerializeField] private Vector2 sniperPatrolX = new Vector2(0f, 4f);
    [Header("Optional")]
    [SerializeField] private bool removeExistingCombatTestEnemies = true;

    private void Start()
    {
        GameObject playerObject = GameObject.Find("player");
        if (playerObject == null) playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null && !playerObject.CompareTag("Player")) playerObject.tag = "Player";
        if (removeExistingCombatTestEnemies)
        {
            GameObject old = GameObject.Find("ShooterBot"); if (old) Destroy(old);
            old = GameObject.Find("TrainingDummy"); if (old) Destroy(old);
        }
        if (FindObjectOfType<GoldCounter>() == null) new GameObject("Gold HUD").AddComponent<GoldCounter>();
        CreateBot("Scout Bot", BotAI.BotType.Scout, scoutSpawn, new Color(.2f, .8f, .3f), playerObject ? playerObject.transform : null, scoutPatrolX);
        CreateBot("Sniper Bot", BotAI.BotType.Sniper, sniperSpawn, new Color(.95f, .25f, .2f), playerObject ? playerObject.transform : null, sniperPatrolX);
        CreateBot("Drone Bot", BotAI.BotType.Drone, droneSpawn, new Color(.2f, .65f, 1f), playerObject ? playerObject.transform : null, Vector2.zero);
    }

    private void CreateBot(string objectName, BotAI.BotType type, Vector2 position, Color color, Transform target, Vector2 patrolX)
    {
        GameObject go = new GameObject(objectName);
        go.transform.position = position;
        go.transform.localScale = new Vector3(.8f, .8f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.White;
        sr.color = color;
        sr.sortingOrder = 2;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = type == BotAI.BotType.Drone ? 0f : 3f;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        go.AddComponent<Health>();
        BotAI ai = go.AddComponent<BotAI>();
        ai.type = type;
        SetPrivate(ai, "player", target);
        if (patrolX != Vector2.zero)
        {
            SetPrivate(ai, "usePatrolBounds", true);
            SetPrivate(ai, "patrolMinX", patrolX.x);
            SetPrivate(ai, "patrolMaxX", patrolX.y);
        }
    }

    private static void SetPrivate<T>(BotAI target, string field, T value)
    {
        var f = typeof(BotAI).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f != null) f.SetValue(target, value);
    }
}