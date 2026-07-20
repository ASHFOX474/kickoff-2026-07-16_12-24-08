using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Optional existing UI panels")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Built-in HUD")]
    public bool drawBuiltInHud = true;
    public bool drawIdentificationMeter = true;
    public string levelTitleOverride = string.Empty;

    public bool IsGameOver { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsGameplayRunning => !IsGameOver && !IsPaused;
    public float HighestIdentification { get; private set; }
    public bool PlayerWon { get; private set; }

    private string resultText = string.Empty;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle meterLabelStyle;
    private GUIStyle levelStyle;
    private GuardAI[] guards = new GuardAI[0];
    private float guardRefreshTimer;
    private Texture2D whiteTexture;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    private void Update()
    {
        if (WasPausePressed() && !IsGameOver)
        {
            TogglePause();
        }

        RefreshIdentificationValue();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f;
        }
    }

    public void OnPlayerCaught()
    {
        PlayerWon = false;
        FinishGame("CAUGHT", losePanel);
    }

    public void OnPlayerIdentified()
    {
        // Identification meter fills up as a warning, but game over is
        // triggered only by physical guard contact (OnPlayerCaught).
        HighestIdentification = 1f;
    }

    public void OnPlayerWin()
    {
        PlayerWon = true;
        FinishGame(IsFinalLevel() ? "CAMPAIGN COMPLETE" : "AREA CLEARED", winPanel);
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        Time.timeScale = 1f;
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;

        if (nextIndex >= 0 && nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    private void FinishGame(string heading, GameObject panel)
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        resultText = heading;
        Time.timeScale = 0f;
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    private void RefreshIdentificationValue()
    {
        guardRefreshTimer -= Time.unscaledDeltaTime;
        if (guardRefreshTimer <= 0f || guards == null || guards.Length == 0)
        {
            guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
            guardRefreshTimer = 0.5f;
        }

        float highest = IsGameOver && !PlayerWon && resultText == "FULLY IDENTIFIED" ? 1f : 0f;
        foreach (GuardAI guard in guards)
        {
            if (guard != null)
            {
                highest = Mathf.Max(highest, guard.DetectionProgress);
            }
        }

        HighestIdentification = highest;
    }

    private void OnGUI()
    {
        if (!drawBuiltInHud)
        {
            return;
        }

        EnsureStyles();
        DrawMissionPanel();

        if (drawIdentificationMeter)
        {
            DrawIdentificationMeter();
        }

        if (!IsGameOver && !IsPaused)
        {
            return;
        }

        float width = 480f;
        float height = PlayerWon ? 370f : 340f;
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUI.color = new Color(0.025f, 0.045f, 0.075f, 0.97f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;

        string heading = IsGameOver ? resultText : "PAUSED";
        GUI.Label(new Rect(panel.x + 20f, panel.y + 28f, width - 40f, 65f), heading, titleStyle);

        float btnX = panel.x + (width - 260f) * 0.5f;
        float btnW = 260f;
        float btnH = 52f;
        float spacing = 62f;
        float firstBtnY = panel.y + 115f;

        if (IsGameOver && PlayerWon)
        {
            string nextLabel = IsFinalLevel() ? "Play Again" : "Next Level";
            if (GUI.Button(new Rect(btnX, firstBtnY, btnW, btnH), nextLabel, buttonStyle))
                LoadNextLevel();

            if (GUI.Button(new Rect(btnX, firstBtnY + spacing, btnW, btnH), "Replay Level", buttonStyle))
                Restart();

            if (GUI.Button(new Rect(btnX, firstBtnY + spacing * 2f, btnW, btnH), "Main Menu", buttonStyle))
                LoadMainMenu();
        }
        else if (IsGameOver)
        {
            if (GUI.Button(new Rect(btnX, firstBtnY, btnW, btnH), "Retry", buttonStyle))
                Restart();

            if (GUI.Button(new Rect(btnX, firstBtnY + spacing, btnW, btnH), "Main Menu", buttonStyle))
                LoadMainMenu();
        }
        else
        {
            // Paused
            if (GUI.Button(new Rect(btnX, firstBtnY, btnW, btnH), "Continue", buttonStyle))
                TogglePause();

            if (GUI.Button(new Rect(btnX, firstBtnY + spacing, btnW, btnH), "Main Menu", buttonStyle))
                LoadMainMenu();
        }
    }

    private void DrawMissionPanel()
    {
        Rect panel = new Rect(14f, 14f, 420f, 90f);
        GUI.color = new Color(0.025f, 0.045f, 0.075f, 0.92f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;

        GUI.Label(new Rect(27f, 20f, 380f, 34f), GetLevelTitle(), levelStyle);
        GUI.Label(new Rect(27f, 58f, 380f, 30f), "WASD / arrows  •  hide  •  reach EXIT", bodyStyle);

        // Pause button — top-right corner of screen
        if (!IsGameOver)
        {
            float btnW = 80f;
            float btnH = 44f;
            if (GUI.Button(new Rect(Screen.width - btnW - 14f, 14f, btnW, btnH),
                IsPaused ? "▶" : "⏸", buttonStyle))
            {
                TogglePause();
            }
        }
    }

    private void DrawIdentificationMeter()
    {
        const float meterWidth = 340f;
        const float meterHeight = 22f;
        float x = (Screen.width - meterWidth) * 0.5f;
        float y = 18f;

        GUI.Label(new Rect(x, y, meterWidth, 22f),
            $"◉  IDENTIFICATION  {Mathf.RoundToInt(HighestIdentification * 100f)}%",
            meterLabelStyle);

        Rect background = new Rect(x, y + 25f, meterWidth, meterHeight);
        Rect fill = new Rect(x + 3f, y + 28f, (meterWidth - 6f) * HighestIdentification, meterHeight - 6f);

        GUI.color = new Color(0.025f, 0.035f, 0.055f, 0.96f);
        GUI.DrawTexture(background, whiteTexture);

        Color safe = new Color(0.18f, 0.82f, 0.48f);
        Color warning = new Color(1f, 0.68f, 0.05f);
        Color danger = new Color(1f, 0.06f, 0.05f);
        GUI.color = HighestIdentification < 0.55f
            ? Color.Lerp(safe, warning, HighestIdentification / 0.55f)
            : Color.Lerp(warning, danger, Mathf.InverseLerp(0.55f, 1f, HighestIdentification));
        GUI.DrawTexture(fill, whiteTexture);
        GUI.color = Color.white;
    }

    private string GetLevelTitle()
    {
        if (!string.IsNullOrWhiteSpace(levelTitleOverride))
        {
            return levelTitleOverride;
        }

        string name = SceneManager.GetActiveScene().name;
        name = name.Replace("Level_", "LEVEL ").Replace("_", " ");
        return name.ToUpperInvariant();
    }

    private bool IsFinalLevel()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        return currentIndex >= SceneManager.sceneCountInBuildSettings - 1;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 38,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        levelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.32f, 0.88f, 1f) }
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 20,
            normal = { textColor = new Color(0.88f, 0.92f, 0.96f) }
        };

        meterLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
    }

    private static bool WasPausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
    }
}
