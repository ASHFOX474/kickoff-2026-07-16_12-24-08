using System;
using UnityEngine;

/// <summary>
/// Draws a filled, wall-clipped vision cone in front of a guard.
/// The cone changes colour and opacity with the guard's current state.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DisallowMultipleComponent]
public class GuardVisionCone : MonoBehaviour
{
    [Header("References")]
    public GuardAI guard;

    [Header("Shape")]
    [Range(8, 96)] public int rayCount = 40;
    [Min(0.02f)] public float originOffset = 0.08f;
    public int sortingOrder = 12;
    public bool showVisionCone = false;

    [Header("Radar colours")]
    public Color patrolColour = new Color(1f, 0.08f, 0.10f, 0.22f);
    public Color alertColour = new Color(1f, 0.32f, 0.04f, 0.31f);
    public Color chaseColour = new Color(1f, 0.02f, 0.04f, 0.42f);
    public Color searchColour = new Color(0.62f, 0.16f, 1f, 0.22f);

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;
    private Vector3[] vertices = Array.Empty<Vector3>();
    private int[] triangles = Array.Empty<int>();

    private void Awake()
    {
        guard = guard != null ? guard : GetComponent<GuardAI>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh { name = "Guard Vision Cone" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Guard Vision Cone Material"
        };
        meshRenderer.sharedMaterial = runtimeMaterial;
        meshRenderer.sortingOrder = sortingOrder;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
            Destroy(mesh);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
            DestroyImmediate(mesh);
        }
    }

    private void LateUpdate()
    {
        if (guard == null)
        {
            guard = GetComponent<GuardAI>();
        }

        if (guard == null)
        {
            meshRenderer.enabled = false;
            return;
        }

        bool gameplayRunning = GameManager.Instance == null || GameManager.Instance.IsGameplayRunning;
        bool visible = gameplayRunning && showVisionCone;
        meshRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        RebuildCone();
        ApplyStateColour();
    }

    private void RebuildCone()
    {
        int safeRayCount = Mathf.Max(8, rayCount);
        int vertexCount = safeRayCount + 2;
        if (vertices.Length != vertexCount)
        {
            vertices = new Vector3[vertexCount];
            triangles = new int[safeRayCount * 3];
        }

        Vector2 origin = (Vector2)transform.position + (Vector2)transform.up * originOffset;
        vertices[0] = transform.InverseTransformPoint(origin);

        float halfAngle = guard.viewAngle * 0.5f;
        for (int i = 0; i <= safeRayCount; i++)
        {
            float t = i / (float)safeRayCount;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * transform.up;
            float distance = FindVisibleDistance(origin, direction, guard.viewDistance);
            Vector2 end = origin + direction.normalized * distance;
            vertices[i + 1] = transform.InverseTransformPoint(end);
        }

        for (int i = 0; i < safeRayCount; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private float FindVisibleDistance(Vector2 origin, Vector2 direction, float maximumDistance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction.normalized, maximumDistance);
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

            if (hit.collider.isTrigger)
            {
                continue;
            }

            return Mathf.Max(0.05f, hit.distance);
        }

        return maximumDistance;
    }

    private void ApplyStateColour()
    {
        Color colour;
        switch (guard.currentState)
        {
            case GuardAI.State.Alert:
                colour = alertColour;
                break;
            case GuardAI.State.Chase:
                colour = chaseColour;
                break;
            case GuardAI.State.Search:
                colour = searchColour;
                break;
            default:
                colour = patrolColour;
                break;
        }

        float identificationBoost = Mathf.Lerp(0.78f, 1.28f, guard.DetectionProgress);
        colour.a = Mathf.Clamp01(colour.a * identificationBoost);
        runtimeMaterial.color = colour;
    }
}
