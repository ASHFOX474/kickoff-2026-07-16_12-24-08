using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The missing front door: Play / How To Play / Quit. Pure OnGUI, same style
/// as GameManager's HUD, so it needs zero art and can't break visually.
/// Put it on an empty GameObject in its own scene (build index 0) via
/// "Kickoff -> -1. Create Main Menu Scene".
/// </summary>
[DisallowMultipleComponent]
public class KickoffMainMenu : MonoBehaviour
{
    public string playSceneName = "Intro";

    private GUIStyle titleStyle, subStyle, buttonStyle, footerStyle, helpStyle;
    private Texture2D whiteTex;
    private float pulse;
    private bool showHelp;

    private void Awake()
    {
        whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
    }

    private void Update()
    {
        pulse += Time.unscaledDeltaTime;
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.02f, 0.04f));

        // faint pitch-stripe backdrop so it isn't just a flat black screen
        for (int i = 0; i < 6; i++)
        {
            float x = Screen.width * (i / 6f);
            DrawRect(new Rect(x, 0, Screen.width / 12f, Screen.height), new Color(1f, 1f, 1f, 0.02f));
        }

        float cx = Screen.width * 0.5f;

        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 70f), "KICKOFF", titleStyle);
        GUI.Label(new Rect(0, Screen.height * 0.18f + 62f, Screen.width, 30f),
            "STEAL THE CUP. DODGE EVERY RIVAL ON THE WAY OUT.", subStyle);

        float btnW = 280f, btnH = 56f;
        float btnX = cx - btnW * 0.5f;
        float btnY = Screen.height * 0.48f;
        float bounce = Mathf.Sin(pulse * 3f) * 3f;

        if (GUI.Button(new Rect(btnX, btnY + bounce, btnW, btnH), "PLAY", buttonStyle))
        {
            SceneManager.LoadScene(playSceneName);
        }

        if (GUI.Button(new Rect(btnX, btnY + 68f, btnW, 44f), showHelp ? "HIDE HELP" : "HOW TO PLAY", buttonStyle))
        {
            showHelp = !showHelp;
        }

        float quitY = btnY + 126f;
        if (showHelp)
        {
            Rect helpRect = new Rect(cx - 240f, quitY, 480f, 118f);
            DrawRect(helpRect, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(helpRect,
                "WASD / Arrows to move.\nStay out of guards' sightlines - duck into hiding spots when you need to.\nGet the cup to the exit before your identification meter fills up.",
                helpStyle);
            quitY += 130f;
        }

        if (GUI.Button(new Rect(btnX, quitY, btnW, 40f), "QUIT", buttonStyle))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        GUI.Label(new Rect(0, Screen.height - 30f, Screen.width, 22f),
            "IUT 12th ICT Fest 2026  -  Theme: Kickoff", footerStyle);
    }

    private void DrawRect(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, whiteTex);
        GUI.color = Color.white;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 52,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.95f, 0.85f, 0.2f) }
        };

        subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };

        helpStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 14,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        footerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            normal = { textColor = new Color(1f, 1f, 1f, 0.4f) }
        };
    }
}