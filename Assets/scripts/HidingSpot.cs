using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A trigger zone that prevents guards from seeing a player inside it.
/// It safely handles players with multiple colliders and overlapping hide zones.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class HidingSpot : MonoBehaviour
{
    private readonly Dictionary<PlayerController, int> occupants = new Dictionary<PlayerController, int>();
    private Collider2D triggerCollider;

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
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        if (!occupants.TryGetValue(player, out int colliderCount))
        {
            occupants[player] = 1;
            player.EnterHidingSpot();
        }
        else
        {
            occupants[player] = colliderCount + 1;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !occupants.TryGetValue(player, out int colliderCount))
        {
            return;
        }

        colliderCount--;
        if (colliderCount <= 0)
        {
            occupants.Remove(player);
            player.ExitHidingSpot();
        }
        else
        {
            occupants[player] = colliderCount;
        }
    }

    private void OnDisable()
    {
        foreach (PlayerController player in occupants.Keys)
        {
            if (player != null)
            {
                player.ExitHidingSpot();
            }
        }

        occupants.Clear();
    }
}
