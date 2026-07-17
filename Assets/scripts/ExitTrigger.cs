using UnityEngine;

/// <summary>Place this on a trigger collider at the maze exit.</summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class ExitTrigger : MonoBehaviour
{
    private bool activated;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        activated = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerWin();
        }
        else
        {
            Debug.LogError("The player reached the exit, but no GameManager exists in the scene.");
        }
    }
}
