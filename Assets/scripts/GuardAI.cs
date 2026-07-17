using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
[DisallowMultipleComponent]
public class GuardAI : MonoBehaviour
{
    public enum State
    {
        Patrol,
        Alert,
        Chase,
        Search
    }

    [Header("Current state")]
    public State currentState = State.Patrol;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    [Min(0.1f)] public float patrolSpeed = 2.2f;
    [Min(0f)] public float waypointWaitTime = 0.3f;
    public bool pingPongPatrol = true;

    [Header("Vision and identification")]
    [Min(0.5f)] public float viewDistance = 6.2f;
    [Range(5f, 180f)] public float viewAngle = 78f;
    [Min(0.1f)] public float identificationTime = 1.65f;
    [Min(0.1f)] public float identificationCooldown = 1.15f;
    [Range(0.05f, 0.8f)] public float alertThreshold = 0.22f;
    [Range(0.1f, 0.95f)] public float chaseThreshold = 0.58f;
    [Min(0f)] public float lostSightGraceTime = 0.55f;

    [Header("Investigation and chase")]
    [Min(0f)] public float alertReactionTime = 0.25f;
    [Min(0.1f)] public float chaseSpeed = 3.6f;
    [Min(0.05f)] public float repathInterval = 0.18f;
    [Min(0.1f)] public float directContactDistance = 0.42f;

    [Header("Search")]
    [Min(0.5f)] public float searchDuration = 7.5f;
    [Range(1, 12)] public int searchRadius = 5;
    [Min(0f)] public float searchPointPause = 0.5f;
    [Min(0f)] public float searchTurnSpeed = 145f;

    [Header("Optional legacy masks")]
    public LayerMask obstacleMask;
    public LayerMask playerMask;

    public float DetectionProgress { get; private set; }
    public bool SeesPlayer { get; private set; }

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private SpriteRenderer spriteRenderer;
    private PlayerController playerController;
    private Transform player;
    private MazeGrid grid;

    private readonly List<Vector2> currentPath = new List<Vector2>();
    private readonly List<Vector2Int> searchCells = new List<Vector2Int>();
    private int pathIndex;
    private int searchIndex;
    private int patrolIndex;
    private int patrolDirection = 1;

    private float stateTimer;
    private float waypointTimer;
    private float repathTimer;
    private float timeSincePlayerSeen;
    private float searchPauseTimer;
    private Vector2 lastKnownPlayerPosition;
    private Vector2Int currentDestinationCell = new Vector2Int(int.MinValue, int.MinValue);
    private Color normalColor = Color.white;
    private System.Random random;

    private bool PathComplete => pathIndex >= currentPath.Count;

    private void Reset()
    {
        ConfigurePhysics();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
        }

        random = new System.Random(GetInstanceID());
        ConfigurePhysics();
    }

    private void Start()
    {
        FindPlayer();
        grid = MazeGrid.Instance;
        IgnoreOtherGuardCollisions();
        BeginPatrol(true);
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
        {
            return;
        }

        if (playerController == null)
        {
            FindPlayer();
            if (playerController == null)
            {
                return;
            }
        }

        SeesPlayer = CanSeePlayer();
        UpdateIdentification(SeesPlayer);
        if (playerController.IsCaught)
        {
            return;
        }

        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol(SeesPlayer);
                break;
            case State.Alert:
                UpdateAlert(SeesPlayer);
                break;
            case State.Chase:
                UpdateChase(SeesPlayer);
                break;
            case State.Search:
                UpdateSearch(SeesPlayer);
                break;
        }

        RefreshStateColour();
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
        {
            StopMoving();
            return;
        }

        switch (currentState)
        {
            case State.Patrol:
                MoveAlongPath(patrolSpeed);
                break;
            case State.Alert:
                StopMoving();
                break;
            case State.Chase:
                MoveAlongPath(chaseSpeed);
                break;
            case State.Search:
                if (searchPauseTimer <= 0f)
                {
                    MoveAlongPath(patrolSpeed * 1.18f);
                }
                else
                {
                    StopMoving();
                }
                break;
        }
    }

    private void LateUpdate()
    {
        KeepVisualUpright();
    }

    private void FindPlayer()
    {
        playerController = FindFirstObjectByType<PlayerController>();
        player = playerController != null ? playerController.transform : null;
        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
        }
    }

    private void UpdateIdentification(bool seesPlayer)
    {
        if (seesPlayer)
        {
            lastKnownPlayerPosition = player.position;
            timeSincePlayerSeen = 0f;
            DetectionProgress += Time.deltaTime / Mathf.Max(0.1f, identificationTime);
            FaceDirection(player.position - transform.position);
        }
        else
        {
            timeSincePlayerSeen += Time.deltaTime;
            DetectionProgress -= Time.deltaTime / Mathf.Max(0.1f, identificationCooldown);
        }

        DetectionProgress = Mathf.Clamp01(DetectionProgress);
        if (DetectionProgress >= 1f && playerController != null && !playerController.IsHidden)
        {
            DetectionProgress = 1f;
            playerController.GetIdentified();
        }
    }

    private void UpdatePatrol(bool seesPlayer)
    {
        if (seesPlayer && DetectionProgress >= alertThreshold)
        {
            BeginAlert();
            return;
        }

        if (!PathComplete)
        {
            waypointTimer = 0f;
            return;
        }

        waypointTimer += Time.deltaTime;
        if (waypointTimer >= waypointWaitTime)
        {
            AdvancePatrolPoint();
            SetDestination(GetCurrentPatrolPosition(), true);
            waypointTimer = 0f;
        }
    }

    private void UpdateAlert(bool seesPlayer)
    {
        stateTimer += Time.deltaTime;

        if (seesPlayer)
        {
            FaceDirection(player.position - transform.position);
            if (DetectionProgress >= chaseThreshold && stateTimer >= alertReactionTime)
            {
                BeginChase();
            }
            return;
        }

        FaceDirection(lastKnownPlayerPosition - rb.position);
        if (timeSincePlayerSeen >= lostSightGraceTime)
        {
            BeginSearch();
        }
    }

    private void UpdateChase(bool seesPlayer)
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            SetDestination(seesPlayer ? (Vector2)player.position : lastKnownPlayerPosition, true);
            repathTimer = repathInterval;
        }

        if (playerController != null && !playerController.IsHidden &&
            Vector2.Distance(rb.position, player.position) <= directContactDistance)
        {
            DetectionProgress = Mathf.Clamp01(
                DetectionProgress + Time.deltaTime / Mathf.Max(0.1f, identificationTime * 0.28f));
            if (DetectionProgress >= 1f)
            {
                playerController.GetIdentified();
                return;
            }
        }

        if (!seesPlayer && timeSincePlayerSeen >= lostSightGraceTime)
        {
            BeginSearch();
        }
    }

    private void UpdateSearch(bool seesPlayer)
    {
        if (seesPlayer)
        {
            if (DetectionProgress >= chaseThreshold)
            {
                BeginChase();
            }
            else
            {
                BeginAlert();
            }
            return;
        }

        stateTimer += Time.deltaTime;
        if (stateTimer >= searchDuration || DetectionProgress <= 0f)
        {
            BeginPatrol(false);
            return;
        }

        if (searchPauseTimer > 0f)
        {
            searchPauseTimer -= Time.deltaTime;
            transform.Rotate(0f, 0f, searchTurnSpeed * Time.deltaTime);
            return;
        }

        if (PathComplete)
        {
            searchPauseTimer = searchPointPause;
            SetNextSearchDestination();
        }
    }

    private void BeginPatrol(bool clearIdentification)
    {
        currentState = State.Patrol;
        stateTimer = 0f;
        timeSincePlayerSeen = 0f;
        waypointTimer = waypointWaitTime;
        if (clearIdentification)
        {
            DetectionProgress = 0f;
        }

        SetDestination(GetCurrentPatrolPosition(), true);
    }

    private void BeginAlert()
    {
        currentState = State.Alert;
        stateTimer = 0f;
        StopMoving();
    }

    private void BeginChase()
    {
        currentState = State.Chase;
        stateTimer = 0f;
        repathTimer = 0f;
        SetDestination(lastKnownPlayerPosition, true);
    }

    private void BeginSearch()
    {
        currentState = State.Search;
        stateTimer = 0f;
        searchPauseTimer = 0f;
        searchIndex = 0;
        BuildSearchCells();
        SetDestination(lastKnownPlayerPosition, true);
    }

    private void BuildSearchCells()
    {
        searchCells.Clear();
        if (grid == null)
        {
            grid = MazeGrid.Instance;
        }

        if (grid == null)
        {
            return;
        }

        Vector2Int center = grid.GetNearestWalkableCell(lastKnownPlayerPosition);
        List<Vector2Int> candidates = grid.GetWalkableCellsInRadius(center, searchRadius);

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            Vector2Int temp = candidates[i];
            candidates[i] = candidates[swapIndex];
            candidates[swapIndex] = temp;
        }

        searchCells.Add(center);
        foreach (Vector2Int candidate in candidates)
        {
            if (candidate != center)
            {
                searchCells.Add(candidate);
            }
        }
    }

    private void SetNextSearchDestination()
    {
        if (searchCells.Count == 0 || grid == null)
        {
            return;
        }

        searchIndex = (searchIndex + 1) % searchCells.Count;
        SetDestination(grid.CellToWorld(searchCells[searchIndex]), true);
    }

    private void AdvancePatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length <= 1)
        {
            patrolIndex = 0;
            return;
        }

        if (!pingPongPatrol)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            return;
        }

        patrolIndex += patrolDirection;
        if (patrolIndex >= patrolPoints.Length)
        {
            patrolIndex = patrolPoints.Length - 2;
            patrolDirection = -1;
        }
        else if (patrolIndex < 0)
        {
            patrolIndex = 1;
            patrolDirection = 1;
        }
    }

    private Vector2 GetCurrentPatrolPosition()
    {
        if (patrolPoints == null || patrolPoints.Length == 0 || patrolPoints[patrolIndex] == null)
        {
            return rb != null ? rb.position : (Vector2)transform.position;
        }

        return patrolPoints[patrolIndex].position;
    }

    private void SetDestination(Vector2 worldDestination, bool force)
    {
        if (grid == null)
        {
            grid = MazeGrid.Instance;
        }

        if (grid == null)
        {
            currentPath.Clear();
            currentPath.Add(worldDestination);
            pathIndex = 0;
            return;
        }

        Vector2Int nextDestinationCell = grid.GetNearestWalkableCell(worldDestination);
        if (!force && nextDestinationCell == currentDestinationCell && !PathComplete)
        {
            return;
        }

        currentDestinationCell = nextDestinationCell;
        currentPath.Clear();
        currentPath.AddRange(grid.FindPathWorld(rb.position, grid.CellToWorld(nextDestinationCell)));
        pathIndex = 0;
    }

    private void MoveAlongPath(float speed)
    {
        if (rb == null || PathComplete)
        {
            StopMoving();
            return;
        }

        Vector2 target = currentPath[pathIndex];
        Vector2 delta = target - rb.position;
        float distance = delta.magnitude;
        float maxStep = speed * Time.fixedDeltaTime;

        if (distance <= 0.04f)
        {
            pathIndex++;
            StopMoving();
            return;
        }

        Vector2 nextPosition = distance <= maxStep
            ? target
            : rb.position + delta.normalized * maxStep;

        rb.MovePosition(nextPosition);
        FaceDirection(delta);

        if (distance <= maxStep + 0.01f)
        {
            pathIndex++;
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null || playerController == null || playerController.IsHidden || playerController.IsCaught)
        {
            return false;
        }

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 toPlayer = (Vector2)player.position - origin;
        float distance = toPlayer.magnitude;

        if (distance > viewDistance)
        {
            return false;
        }

        if (distance <= 0.001f)
        {
            return true;
        }

        float angle = Vector2.Angle(transform.up, toPlayer);
        if (angle > viewAngle * 0.5f)
        {
            return false;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, toPlayer.normalized, distance + 0.05f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<GuardAI>() != null)
            {
                continue;
            }

            PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
            if (hitPlayer != null)
            {
                return hitPlayer == playerController;
            }

            if (hit.collider.isTrigger)
            {
                continue;
            }

            return false;
        }

        return false;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        PlayerController collidedPlayer = collision.collider.GetComponentInParent<PlayerController>();
        if (collidedPlayer == null || collidedPlayer.IsHidden)
        {
            return;
        }

        DetectionProgress = Mathf.Clamp01(
            DetectionProgress + Time.fixedDeltaTime / Mathf.Max(0.1f, identificationTime * 0.24f));
        if (DetectionProgress >= 1f)
        {
            collidedPlayer.GetIdentified();
        }
    }

    private void IgnoreOtherGuardCollisions()
    {
        if (ownCollider == null)
        {
            return;
        }

        GuardAI[] guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        foreach (GuardAI other in guards)
        {
            if (other == this)
            {
                continue;
            }

            Collider2D otherCollider = other.GetComponent<Collider2D>();
            if (otherCollider != null)
            {
                Physics2D.IgnoreCollision(ownCollider, otherCollider, true);
            }
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
        body.mass = 50f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void StopMoving()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.up = direction.normalized;
        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
        {
            spriteRenderer.flipX = direction.x < 0f;
        }

        KeepVisualUpright();
    }

    private void KeepVisualUpright()
    {
        if (spriteRenderer != null && spriteRenderer.transform != transform)
        {
            spriteRenderer.transform.rotation = Quaternion.identity;
        }
    }

    private void RefreshStateColour()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        switch (currentState)
        {
            case State.Patrol:
                spriteRenderer.color = Color.Lerp(normalColor, new Color(1f, 0.82f, 0.18f), DetectionProgress * 0.72f);
                break;
            case State.Alert:
                spriteRenderer.color = Color.Lerp(normalColor, new Color(1f, 0.78f, 0.08f), 0.72f);
                break;
            case State.Chase:
                spriteRenderer.color = Color.Lerp(normalColor, new Color(1f, 0.12f, 0.12f), 0.82f);
                break;
            case State.Search:
                spriteRenderer.color = Color.Lerp(normalColor, new Color(0.72f, 0.25f, 1f), 0.55f);
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0f, 0f, viewAngle * 0.5f) * transform.up;
        Vector3 right = Quaternion.Euler(0f, 0f, -viewAngle * 0.5f) * transform.up;
        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);

        Gizmos.color = Color.red;
        for (int i = pathIndex; i < currentPath.Count; i++)
        {
            Gizmos.DrawSphere(currentPath[i], 0.07f);
            if (i > pathIndex)
            {
                Gizmos.DrawLine(currentPath[i - 1], currentPath[i]);
            }
        }
    }
}
