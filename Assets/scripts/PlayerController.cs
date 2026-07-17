using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Min(0.1f)] public float moveSpeed = 4.5f;

    [Header("Hidden feedback")]
    [Range(0.1f, 1f)] public float hiddenAlpha = 0.35f;

    public bool IsHidden => hidingSpotCount > 0;
    public bool IsCaught { get; private set; }
    public Vector2 CurrentMoveInput => moveInput;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    private int hidingSpotCount;

    private void Reset()
    {
        ConfigurePhysics();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ConfigurePhysics();
    }

    private void Update()
    {
        if (IsCaught || (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning))
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = ReadMovementInput();
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        if (spriteRenderer != null && Mathf.Abs(moveInput.x) > 0.01f)
        {
            spriteRenderer.flipX = moveInput.x < 0f;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = IsCaught ? Vector2.zero : moveInput * moveSpeed;
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void EnterHidingSpot()
    {
        hidingSpotCount++;
        RefreshHiddenVisual();
    }

    public void ExitHidingSpot()
    {
        hidingSpotCount = Mathf.Max(0, hidingSpotCount - 1);
        RefreshHiddenVisual();
    }

    // Compatibility with the original project.
    public void SetHidden(bool hidden)
    {
        if (hidden)
        {
            EnterHidingSpot();
        }
        else
        {
            ExitHidingSpot();
        }
    }

    public void GetCaught()
    {
        EndRun(false);
    }

    public void GetIdentified()
    {
        EndRun(true);
    }

    private void EndRun(bool identified)
    {
        if (IsCaught || IsHidden)
        {
            return;
        }

        IsCaught = true;
        moveInput = Vector2.zero;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("No GameManager exists in the scene.");
            return;
        }

        if (identified)
        {
            GameManager.Instance.OnPlayerIdentified();
        }
        else
        {
            GameManager.Instance.OnPlayerCaught();
        }
    }

    private void ConfigurePhysics()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            return;
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void RefreshHiddenVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = spriteRenderer.color;
        color.a = IsHidden ? hiddenAlpha : 1f;
        spriteRenderer.color = color;
    }

    private static Vector2 ReadMovementInput()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 result = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) result.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) result.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) result.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) result.y += 1f;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.sqrMagnitude > result.sqrMagnitude)
            {
                result = stick;
            }
        }

        return result;
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }
}
