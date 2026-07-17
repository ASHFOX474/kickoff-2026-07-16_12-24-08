using UnityEngine;

public class GuardAI : MonoBehaviour
{
    public enum State { Patrol, Alert, Chase, Search }
    public State currentState = State.Patrol;

    [Header("Patrol")]
    public Transform[] patrolPoints;   // linear back-and-forth waypoints
    public float patrolSpeed = 2f;
    private int patrolIndex = 0;
    private bool patrolForward = true;

    [Header("Vision")]
    public float viewDistance = 5f;
    public float viewAngle = 60f;      // total cone angle in degrees
    public LayerMask obstacleMask;      // walls block sight
    public LayerMask playerMask;

    [Header("Chase/Search")]
    public float chaseSpeed = 3.5f;
    public float alertReactionTime = 0.5f; // time before alert becomes chase
    public float searchDuration = 3f;

    private float stateTimer;
    private Vector3 lastKnownPlayerPos;
    private Transform player;
    private PlayerController playerController;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerController = p.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Patrol: DoPatrol(); CheckVision(); break;
            case State.Alert: DoAlert(); break;
            case State.Chase: DoChase(); break;
            case State.Search: DoSearch(); break;
        }
    }

    void DoPatrol()
    {
        if (patrolPoints.Length == 0) return;
        Transform target = patrolPoints[patrolIndex];
        transform.position = Vector3.MoveTowards(transform.position, target.position, patrolSpeed * Time.deltaTime);

        // face movement direction (used for vision cone)
        Vector3 dir = target.position - transform.position;
        if (dir.sqrMagnitude > 0.001f) FaceDirection(dir);

        if (Vector3.Distance(transform.position, target.position) < 0.05f)
        {
            if (patrolForward)
            {
                patrolIndex++;
                if (patrolIndex >= patrolPoints.Length) { patrolIndex = patrolPoints.Length - 2; patrolForward = false; }
            }
            else
            {
                patrolIndex--;
                if (patrolIndex < 0) { patrolIndex = 1; patrolForward = true; }
            }
        }
    }

    void CheckVision()
    {
        if (player == null || playerController == null) return;
        if (playerController.IsHidden) return; // can't be seen while hidden

        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return;

        float angle = Vector3.Angle(transform.up, toPlayer); // assumes "up" is guard's facing dir
        if (angle > viewAngle * 0.5f) return;

        // line of sight check - walls block it
        RaycastHit2D hit = Physics2D.Raycast(transform.position, toPlayer.normalized, dist, obstacleMask | playerMask);
        if (hit.collider != null && ((1 << hit.collider.gameObject.layer) & playerMask) != 0)
        {
            lastKnownPlayerPos = player.position;
            currentState = State.Alert;
            stateTimer = 0f;
        }
    }

    void DoAlert()
    {
        stateTimer += Time.deltaTime;
        // optional: show "!" icon here
        if (stateTimer >= alertReactionTime)
        {
            currentState = State.Chase;
        }
    }

    void DoChase()
    {
        if (player != null && !playerController.IsHidden)
        {
            lastKnownPlayerPos = player.position;
            transform.position = Vector3.MoveTowards(transform.position, lastKnownPlayerPos, chaseSpeed * Time.deltaTime);
            FaceDirection(lastKnownPlayerPos - transform.position);

            if (Vector3.Distance(transform.position, player.position) < 0.4f)
            {
                playerController.GetCaught();
            }
        }
        else
        {
            // lost sight (player hid) - go search last known position
            currentState = State.Search;
            stateTimer = 0f;
            transform.position = Vector3.MoveTowards(transform.position, lastKnownPlayerPos, chaseSpeed * Time.deltaTime);
        }
    }

    void DoSearch()
    {
        stateTimer += Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, lastKnownPlayerPos, patrolSpeed * Time.deltaTime);
        if (stateTimer >= searchDuration)
        {
            currentState = State.Patrol; // give up, resume patrol
        }
    }

    void FaceDirection(Vector3 dir)
    {
        dir.Normalize();
        transform.up = dir; // works for 2D top-down sprites facing "up" by default
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0, 0, viewAngle * 0.5f) * transform.up;
        Vector3 right = Quaternion.Euler(0, 0, -viewAngle * 0.5f) * transform.up;
        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);
    }
}
