using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI - assign panels in inspector")]
    public GameObject winPanel;
    public GameObject losePanel;

    private bool gameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void OnPlayerCaught()
    {
        if (gameOver) return;
        gameOver = true;
        Time.timeScale = 0f;
        if (losePanel != null) losePanel.SetActive(true);
    }

    // Call this from the exit trigger (put a trigger collider at the maze exit)
    public void OnPlayerWin()
    {
        if (gameOver) return;
        gameOver = true;
        Time.timeScale = 0f;
        if (winPanel != null) winPanel.SetActive(true);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
