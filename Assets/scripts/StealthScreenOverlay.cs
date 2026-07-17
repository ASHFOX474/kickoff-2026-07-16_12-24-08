using UnityEngine;

/// <summary>
/// Adds a subtle vignette and a red danger pulse as identification approaches 100%.
/// It uses IMGUI so no extra Canvas setup is required.
/// </summary>
[DisallowMultipleComponent]
public class StealthScreenOverlay : MonoBehaviour
{
    [Range(0f, 1f)] public float vignetteStrength = 0.72f;
    [Range(0f, 1f)] public float dangerStrength = 0.55f;

    private Texture2D vignetteTexture;
    private Texture2D solidTexture;

    private void Awake()
    {
        vignetteTexture = BuildVignetteTexture(256);
        solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        solidTexture.SetPixel(0, 0, Color.white);
        solidTexture.Apply();
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            Destroy(vignetteTexture);
            Destroy(solidTexture);
        }
        else
        {
            DestroyImmediate(vignetteTexture);
            DestroyImmediate(solidTexture);
        }
    }

    private void OnGUI()
    {
        GUI.depth = 100;
        if (vignetteTexture != null)
        {
            GUI.color = new Color(1f, 1f, 1f, vignetteStrength);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignetteTexture, ScaleMode.StretchToFill);
        }

        float identification = GameManager.Instance != null ? GameManager.Instance.HighestIdentification : 0f;
        if (identification > 0.55f && solidTexture != null)
        {
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 7f) * 0.5f;
            float alpha = Mathf.InverseLerp(0.55f, 1f, identification) * dangerStrength * Mathf.Lerp(0.4f, 1f, pulse);
            GUI.color = new Color(1f, 0.02f, 0.02f, alpha);

            float border = Mathf.Lerp(4f, 18f, identification);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, border), solidTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - border, Screen.width, border), solidTexture);
            GUI.DrawTexture(new Rect(0f, 0f, border, Screen.height), solidTexture);
            GUI.DrawTexture(new Rect(Screen.width - border, 0f, border, Screen.height), solidTexture);
        }

        GUI.color = Color.white;
    }

    private static Texture2D BuildVignetteTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Stealth Vignette",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maximumDistance = center.magnitude;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / maximumDistance;
                float alpha = Mathf.SmoothStep(0f, 0.82f, Mathf.InverseLerp(0.42f, 1f, normalizedDistance));
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
