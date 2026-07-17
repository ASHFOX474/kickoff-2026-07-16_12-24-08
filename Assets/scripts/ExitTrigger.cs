using UnityEngine;

// Put this on a trigger collider at the maze exit
[RequireComponent(typeof(Collider2D))]
public class ExitTrigger : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.OnPlayerWin();
        }
    }
}
