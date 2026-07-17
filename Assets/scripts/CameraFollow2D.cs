using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;

    [Header("Follow")]
    [Min(0f)] public float smoothTime = 0.10f;
    public bool clampToMaze = true;

    [Header("Player point of view")]
    [Min(2f)] public float normalZoom = 5.35f;
    [Min(2f)] public float dangerZoom = 4.80f;
    [Min(0.1f)] public float zoomResponse = 5f;
    [Range(0f, 1f)] public float lookAheadStrength = 0.38f;

    private Camera attachedCamera;
    private Vector3 velocity;
    private PlayerController playerController;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        attachedCamera.orthographic = true;
        attachedCamera.orthographicSize = normalZoom;
    }

    private void LateUpdate()
    {
        ResolveTarget();
        if (target == null)
        {
            return;
        }

        float identification = GameManager.Instance != null ? GameManager.Instance.HighestIdentification : 0f;
        float desiredZoom = Mathf.Lerp(normalZoom, dangerZoom, identification);
        attachedCamera.orthographicSize = Mathf.Lerp(
            attachedCamera.orthographicSize,
            desiredZoom,
            1f - Mathf.Exp(-zoomResponse * Time.unscaledDeltaTime));

        Vector2 lookAhead = Vector2.zero;
        if (playerController != null)
        {
            lookAhead = playerController.CurrentMoveInput * lookAheadStrength;
        }

        Vector3 desired = new Vector3(
            target.position.x + lookAhead.x,
            target.position.y + lookAhead.y,
            transform.position.z);
        desired = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        if (clampToMaze && MazeGrid.Instance != null)
        {
            Bounds bounds = MazeGrid.Instance.WorldBounds;
            float verticalExtent = attachedCamera.orthographicSize;
            float horizontalExtent = verticalExtent * attachedCamera.aspect;

            float minX = bounds.min.x + horizontalExtent;
            float maxX = bounds.max.x - horizontalExtent;
            float minY = bounds.min.y + verticalExtent;
            float maxY = bounds.max.y - verticalExtent;

            desired.x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : bounds.center.x;
            desired.y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : bounds.center.y;
        }

        transform.position = desired;
    }

    private void ResolveTarget()
    {
        if (target != null)
        {
            if (playerController == null)
            {
                playerController = target.GetComponent<PlayerController>();
            }
            return;
        }

        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            target = playerController.transform;
        }
    }
}
