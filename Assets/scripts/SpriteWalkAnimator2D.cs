using UnityEngine;

/// <summary>
/// Makes a top-down character actually look like it's walking instead of gliding.
/// Reads velocity straight off the Rigidbody2D, so it needs zero changes to
/// PlayerController or GuardAI. Two things happen while moving:
///  1) a subtle vertical bob (always works, no art required)
///  2) a frame swap to a "_Walk" sprite next to the base sprite in Resources,
///     if one exists (falls back gracefully to just the bob if it doesn't).
/// Attach via Initialize(resourcePath, rigidbody) right after you create the character.
/// </summary>
[DisallowMultipleComponent]
public class SpriteWalkAnimator2D : MonoBehaviour
{
    private const float MinSpeedToAnimate = 0.15f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Sprite idleSprite;
    private Sprite walkSprite;
    private Transform visualTransform;
    private Vector3 restLocalPosition;

    private float frameTimer;
    private bool onWalkFrame;
    private float bobPhase;

    /// <param name="baseResourcePath">e.g. "Sprites/PlayerPatrol" - the same path already used to load the idle sprite. This component looks for "Sprites/PlayerPatrol_Walk" next to it.</param>
    public void Initialize(string baseResourcePath, Rigidbody2D rigidbody2D)
    {
        body = rigidbody2D;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            idleSprite = spriteRenderer.sprite;
            visualTransform = spriteRenderer.transform;
            restLocalPosition = visualTransform.localPosition;
        }

        if (!string.IsNullOrEmpty(baseResourcePath))
        {
            walkSprite = Resources.Load<Sprite>(baseResourcePath + "_Walk");
        }
    }

    private void Update()
    {
        if (spriteRenderer == null || body == null)
        {
            return;
        }

        float speed = body.linearVelocity.magnitude;

        if (speed > MinSpeedToAnimate)
        {
            float stepRate = Mathf.Lerp(4f, 9f, Mathf.Clamp01(speed / 5f));
            frameTimer += Time.deltaTime * stepRate;
            if (frameTimer >= 1f)
            {
                frameTimer = 0f;
                onWalkFrame = !onWalkFrame;
                if (walkSprite != null)
                {
                    spriteRenderer.sprite = onWalkFrame ? walkSprite : idleSprite;
                }
            }

            bobPhase += Time.deltaTime * Mathf.Lerp(8f, 14f, Mathf.Clamp01(speed / 5f));
            float bob = Mathf.Sin(bobPhase) * 0.035f;
            if (visualTransform != null)
            {
                visualTransform.localPosition = restLocalPosition + new Vector3(0f, bob, 0f);
            }
        }
        else
        {
            frameTimer = 0f;
            onWalkFrame = false;
            bobPhase = 0f;
            if (spriteRenderer.sprite != idleSprite)
            {
                spriteRenderer.sprite = idleSprite;
            }
            if (visualTransform != null)
            {
                visualTransform.localPosition = restLocalPosition;
            }
        }
    }
}
