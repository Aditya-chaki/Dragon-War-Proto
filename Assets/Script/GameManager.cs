using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject SkillPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Game Over UI Elements")]
    [SerializeField] private TextMeshProUGUI winnerText;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button retryButton;

    [Header("Players")]
    [SerializeField] private TopDownPlayerController player;      // Human Player (PlayerHealth)
    [SerializeField] private TopDownPlayerController enemy;       // AI Enemy (EnemyHealth)

    private bool gameIsActive = false;

    void Awake()
    {
        // Validate references
        if (player == null || enemy == null)
        {
            Debug.LogError("GameManager missing Player or Enemy references!", this);
            enabled = false;
            return;
        }

        // Button listeners
        if (playButton != null)
            playButton.onClick.AddListener(StartGame);
        if (retryButton != null)
            retryButton.onClick.AddListener(RestartGame);

        ShowMainMenu();
    }

    void Update()
    {
        if (!gameIsActive) return;

        // Check deaths (handles both SetActive(false) and Destroy())
        bool playerDead = (player == null || !player.gameObject.activeInHierarchy);
        bool enemyDead = (enemy == null || !enemy.gameObject.activeInHierarchy);

        if (playerDead || enemyDead)
        {
            if (playerDead && enemyDead)
            {
                ShowGameOver("It's a Draw!");
            }
            else if (playerDead)
            {
                ShowGameOver("Enemy Wins!");
            }
            else // enemyDead
            {
                ShowGameOver("Player Wins!");
            }
        }
    }

    private void ShowMainMenu()
    {
        gameIsActive = false;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (SkillPanel != null) SkillPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Time.timeScale = 0f;
    }

    private void StartGame()
    {
        gameIsActive = true;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (SkillPanel != null) SkillPanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void ShowGameOver(string message)
    {
        gameIsActive = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (SkillPanel != null) SkillPanel.SetActive(false);
        if (winnerText != null) winnerText.text = message;
        Time.timeScale = 0f;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}