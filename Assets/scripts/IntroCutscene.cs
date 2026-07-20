using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Opening cinematic: the World Cup on its pedestal, two hands steal it from the dark,
/// then the reveal - it's your player, in Spain's red and navy, holding the cup.
/// Drawn entirely with OnGUI primitives so it needs zero extra art assets and can't
/// go missing/pink in a last-minute build. Advance with any key/click, or it auto-advances.
/// Put this on an empty GameObject in its own scene (build index after the menu) and set
/// nextSceneName to your first playable level.
/// </summary>
[DisallowMultipleComponent]
public class IntroCutscene : MonoBehaviour
{
    [Header("Flow")]
    public string nextSceneName = "Level_01_Security_Facility";
    public bool allowSkip = true;

    [Header("Timing (seconds per beat)")]
    public float beatSpotlight = 2.2f;
    public float beatHandsReach = 1.8f;
    public float beatTheft = 1.0f;
    public float beatBlackout = 0.6f;
    public float beatReveal = 2.4f;
    public float beatTitle = 2.4f;

    [Header("Look")]
    [Range(0f, 0.2f)] public float letterboxHeight01 = 0.09f;

    private enum Beat { Spotlight, HandsReach, Theft, Blackout, Reveal, Title, Done }

    private Beat beat = Beat.Spotlight;
    private float beatTimer;
    private float handReach01;
    private Texture2D whiteTex;
    private GUIStyle titleStyle;
    private GUIStyle subStyle;
    private GUIStyle skipStyle;

    private void Awake()
    {
        whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
    }

    private void Update()
    {
        beatTimer += Time.deltaTime;

        switch (beat)
        {
            case Beat.Spotlight:
                if (beatTimer >= beatSpotlight) NextBeat();
                break;
            case Beat.HandsReach:
                handReach01 = Mathf.Clamp01(beatTimer / beatHandsReach);
                if (beatTimer >= beatHandsReach) NextBeat();
                break;
            case Beat.Theft:
                if (beatTimer >= beatTheft) NextBeat();
                break;
            case Beat.Blackout:
                if (beatTimer >= beatBlackout) NextBeat();
                break;
            case Beat.Reveal:
                if (beatTimer >= beatReveal) NextBeat();
                break;
            case Beat.Title:
                if (beatTimer >= beatTitle) NextBeat();
                break;
            case Beat.Done:
                LoadNext();
                break;
        }

        if (allowSkip && beat != Beat.Done && WasAdvancePressed())
        {
            LoadNext();
        }
    }

    private void NextBeat()
    {
        beat++;
        beatTimer = 0f;
    }

    private void LoadNext()
    {
        beat = Beat.Done;
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawRect(new Rect(0, 0, Screen.width, Screen.height), Color.black);

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        switch (beat)
        {
            case Beat.Spotlight:
                DrawSpotlight(cx, cy, 90f);
                DrawTrophy(cx, cy, 1f);
                DrawCaption("A STADIUM VAULT. THE NIGHT BEFORE THE FINAL.", 0.85f);
                break;

            case Beat.HandsReach:
                DrawSpotlight(cx, cy, 90f);
                DrawTrophy(cx, cy, 1f);
                DrawHands(cx, cy, handReach01);
                DrawCaption("SOMETHING MOVES IN THE DARK.", 0.85f);
                break;

            case Beat.Theft:
                DrawSpotlight(cx, cy, 90f * (1f - beatTimer / beatTheft));
                DrawHands(cx, cy, 1f);
                break;

            case Beat.Blackout:
                // pure black beat, silence before the reveal
                break;

            case Beat.Reveal:
                {
                    float t = Mathf.Clamp01(beatTimer / 0.6f);
                    DrawSpotlight(cx, cy, Mathf.Lerp(0f, 130f, t));
                    DrawSpainFigure(cx, cy, t);
                    DrawTrophy(cx, cy - 130f, Mathf.Clamp01((beatTimer - 0.3f) / 0.5f));
                    DrawCaption("IT IS YAMAL.", Mathf.Clamp01(beatTimer - 0.9f));
                }
                break;

            case Beat.Title:
                DrawSpotlight(cx, cy, 130f);
                DrawSpainFigure(cx, cy, 1f);
                DrawTrophy(cx, cy - 130f, 1f);
                DrawBigTitle("THE CUP IS OUT.", "GET IT PAST EVERY TEAM THAT WANTS IT BACK.");
                break;
        }

        DrawLetterbox();

        if (allowSkip && beat != Beat.Done)
        {
            GUI.Label(new Rect(Screen.width - 170f, Screen.height - 40f, 150f, 30f), "click / key to skip", skipStyle);
        }
    }

    // ---------- primitive "actors" drawn with GUI rects/labels, no art required ----------

    private void DrawLetterbox()
    {
        if (letterboxHeight01 <= 0f) return;
        float h = Screen.height * letterboxHeight01;
        DrawRect(new Rect(0, 0, Screen.width, h), Color.black);
        DrawRect(new Rect(0, Screen.height - h, Screen.width, h), Color.black);
    }

    private void DrawSpotlight(float cx, float cy, float radius)
    {
        if (radius <= 0f) return;
        int rings = 10;
        for (int i = rings; i >= 1; i--)
        {
            float t = i / (float)rings;
            float r = radius * t;
            float alpha = 0.05f * (1f - t) + 0.02f;
            DrawRect(new Rect(cx - r, cy - r, r * 2f, r * 2f), new Color(1f, 0.95f, 0.75f, alpha));
        }
    }

    private void DrawTrophy(float cx, float cy, float alpha01)
    {
        if (alpha01 <= 0f) return;
        Color gold = new Color(1f, 0.82f, 0.15f, alpha01);
        DrawRect(new Rect(cx - 18f, cy + 40f, 36f, 8f), gold);
        DrawRect(new Rect(cx - 4f, cy + 10f, 8f, 32f), gold);
        DrawRect(new Rect(cx - 16f, cy - 4f, 32f, 16f), gold);
        DrawRect(new Rect(cx - 22f, cy - 18f, 44f, 16f), gold);
        DrawRect(new Rect(cx - 14f, cy - 34f, 28f, 18f), gold);
    }

    private void DrawHands(float cx, float cy, float reach01)
    {
        if (reach01 <= 0f) return;
        Color skin = new Color(0.55f, 0.35f, 0.22f, 1f);
        float offset = Mathf.Lerp(260f, 30f, reach01);
        DrawRect(new Rect(cx - offset - 20f, cy - 10f, 40f, 20f), skin);
        DrawRect(new Rect(cx - offset - 30f, cy - 30f, 20f, 40f), skin);
        DrawRect(new Rect(cx + offset - 20f, cy - 10f, 40f, 20f), skin);
        DrawRect(new Rect(cx + offset + 10f, cy - 30f, 20f, 40f), skin);
    }

    private void DrawSpainFigure(float cx, float cy, float alpha01)
    {
        if (alpha01 <= 0f) return;
        Color red = new Color(0.78f, 0.06f, 0.18f, alpha01);
        Color navy = new Color(0.1f, 0.16f, 0.29f, alpha01);
        Color skin = new Color(0.86f, 0.62f, 0.42f, alpha01);
        Color hair = new Color(0.2f, 0.13f, 0.08f, alpha01);

        // legs (navy shorts + socks)
        DrawRect(new Rect(cx - 20f, cy + 60f, 16f, 50f), navy);
        DrawRect(new Rect(cx + 4f, cy + 60f, 16f, 50f), navy);
        // torso (solid red shirt)
        DrawRect(new Rect(cx - 20f, cy - 10f, 40f, 70f), red);
        // arms raised (holding the cup up)
        DrawRect(new Rect(cx - 34f, cy - 40f, 14f, 40f), skin);
        DrawRect(new Rect(cx + 20f, cy - 40f, 14f, 40f), skin);
        // head
        DrawRect(new Rect(cx - 14f, cy - 40f, 28f, 28f), skin);
        DrawRect(new Rect(cx - 14f, cy - 46f, 28f, 10f), hair);
    }

    private void DrawCaption(string text, float alpha01)
    {
        if (alpha01 <= 0f) return;
        Color c = subStyle.normal.textColor;
        c.a = alpha01;
        subStyle.normal.textColor = c;
        GUI.Label(new Rect(0, Screen.height - 120f, Screen.width, 40f), text, subStyle);
        c.a = 1f;
        subStyle.normal.textColor = c;
    }

    private void DrawBigTitle(string line1, string line2)
    {
        GUI.Label(new Rect(0, Screen.height - 170f, Screen.width, 50f), line1, titleStyle);
        GUI.Label(new Rect(0, Screen.height - 110f, Screen.width, 40f), line2, subStyle);
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
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
        };

        skipStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 12,
            normal = { textColor = new Color(1f, 1f, 1f, 0.5f) }
        };
    }

    private static bool WasAdvancePressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return key || click;
#else
        return Input.anyKeyDown;
#endif
    }
}
