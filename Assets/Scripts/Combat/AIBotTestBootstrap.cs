using UnityEngine;

/// <summary>Test scene helper. Uses the existing player and map-editor geometry; it does not create platforms.</summary>
public class AIBotTestBootstrap : MonoBehaviour
{
    [Header("Bot spawn positions")]
    [SerializeField] private Vector2 scoutSpawn = new Vector2(-4f, 0f);
    [SerializeField] private Vector2 sniperSpawn = new Vector2(2f, 0f);
    [SerializeField] private Vector2 droneSpawn = new Vector2(8f, 3f);
    [Header("Patrol limits")]
    [SerializeField] private Vector2 scoutPatrolX = new Vector2(-6f, 0f);
    [SerializeField] private Vector2 sniperPatrolX = new Vector2(0f, 4f);
    [SerializeField] private Vector2 dronePatrolX = new Vector2(5f, 11f);
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
        CreateBot("Scout Bot", TestBotAI.BotType.Scout, scoutSpawn, new Color(.2f, .8f, .3f), playerObject ? playerObject.transform : null, scoutPatrolX);
        CreateBot("Sniper Bot", TestBotAI.BotType.Sniper, sniperSpawn, new Color(.95f, .25f, .2f), playerObject ? playerObject.transform : null, sniperPatrolX);
        CreateBot("Drone Bot", TestBotAI.BotType.Drone, droneSpawn, new Color(.2f, .65f, 1f), playerObject ? playerObject.transform : null, dronePatrolX);
    }

    private void CreateBot(string objectName, TestBotAI.BotType type, Vector2 position, Color color, Transform target, Vector2 patrolX)
    {
        GameObject go = new GameObject(objectName); go.transform.position = position; go.transform.localScale = new Vector3(.8f, .8f, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>(); sr.sprite = RuntimeSprites.White; sr.color = color; sr.sortingOrder = 2;
        BoxCollider2D box = go.AddComponent<BoxCollider2D>(); box.size = Vector2.one;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0f; rb.bodyType = RigidbodyType2D.Kinematic;
        Health health = go.AddComponent<Health>();
        TestBotAI ai = go.AddComponent<TestBotAI>();
        // Serialized values remain editable on the component; runtime setup only supplies type/target/spawn defaults.
        SetPrivate(ai, "type", type); SetPrivate(ai, "player", target); SetPrivate(ai, "patrolMinX", patrolX.x); SetPrivate(ai, "patrolMaxX", patrolX.y);
    }
    private static void SetPrivate<T>(TestBotAI target, string field, T value)
    {
        var f = typeof(TestBotAI).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f != null) f.SetValue(target, value);
    }
}
