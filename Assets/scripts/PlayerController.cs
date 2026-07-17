using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public bool IsHidden { get; private set; }

    private Rigidbody2D rb;
    private Vector2 input;
    private SpriteRenderer sr;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        input = input.normalized;
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + input * moveSpeed * Time.fixedDeltaTime);
    }

    // Called by HidingSpot triggers
    public void SetHidden(bool hidden)
    {
        IsHidden = hidden;
        if (sr != null)
        {
            // visually dim/fade while hidden so player gets feedback
            Color c = sr.color;
            c.a = hidden ? 0.35f : 1f;
            sr.color = c;
        }
    }

    public void GetCaught()
    {
        Debug.Log("Player caught!");
        GameManager.Instance.OnPlayerCaught();
    }
}
