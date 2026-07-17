using UnityEngine;

// Attach to any hideaway: box, closet, behind-door zone, etc.
[RequireComponent(typeof(Collider2D))]
public class HidingSpot : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null) pc.SetHidden(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null) pc.SetHidden(false);
    }
}
