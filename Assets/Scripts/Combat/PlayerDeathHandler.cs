using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 사망 처리: 체력 0칸 → (입력 잠금은 PlayerController2D가 처리) → 잠시 후 "R : 다시 시작" 안내.
/// 사망 애니메이션 재생은 PlayerCombatAnimator(Die 트리거)가 담당한다.
/// 나중에 결과 화면/정원 복귀 흐름이 생기면 이 파일의 Restart()만 바꾸면 된다.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDeathHandler : MonoBehaviour
{
    [Tooltip("사망 후 재시작 안내가 뜨기까지의 시간(초). 사망 애니메이션 길이에 맞추면 좋음")]
    [SerializeField] private float promptDelay = 1.2f;

    private Health health;
    private bool dead;
    private float deadAt;

    private bool PromptVisible { get { return dead && Time.unscaledTime - deadAt >= promptDelay; } }

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
    }

    private void OnDied(Health h)
    {
        dead = true;
        deadAt = Time.unscaledTime;
    }

    private void Update()
    {
        if (PromptVisible && Input.GetKeyDown(KeyCode.R)) Restart();
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        if (!PromptVisible) return;

        GUIStyle big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 48,
            fontStyle = FontStyle.Bold
        };
        big.normal.textColor = new Color(1f, 0.3f, 0.3f);
        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 80), "YOU DIED", big);

        big.fontSize = 22;
        big.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.35f + 80, Screen.width, 40), "R : 다시 시작", big);
    }
}
