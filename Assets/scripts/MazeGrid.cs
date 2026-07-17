using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the generated maze's walkable cells and provides lightweight A* pathfinding.
/// The whole maze is small, so a list-based open set is fast enough and avoids package dependencies.
/// </summary>
[DisallowMultipleComponent]
public sealed class MazeGrid : MonoBehaviour
{
    public static MazeGrid Instance { get; private set; }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public float CellSize { get; private set; }
    public Vector2 Origin { get; private set; }
    public Bounds WorldBounds { get; private set; }

    private bool[,] walkable;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Initialize(bool[,] map, float cellSize, Vector2 origin)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        walkable = map;
        Width = map.GetLength(0);
        Height = map.GetLength(1);
        CellSize = Mathf.Max(0.1f, cellSize);
        Origin = origin;

        Vector2 center = Origin + new Vector2(
            (Width - 1) * CellSize * 0.5f,
            (Height - 1) * CellSize * 0.5f);

        WorldBounds = new Bounds(center, new Vector3(Width * CellSize, Height * CellSize, 0f));
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return walkable != null && IsInside(cell) && walkable[cell.x, cell.y];
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return Origin + new Vector2(cell.x * CellSize, cell.y * CellSize);
    }

    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        int x = Mathf.RoundToInt((worldPosition.x - Origin.x) / CellSize);
        int y = Mathf.RoundToInt((worldPosition.y - Origin.y) / CellSize);
        return new Vector2Int(x, y);
    }

    public Vector2Int GetNearestWalkableCell(Vector2 worldPosition)
    {
        Vector2Int requested = WorldToCell(worldPosition);
        requested.x = Mathf.Clamp(requested.x, 0, Width - 1);
        requested.y = Mathf.Clamp(requested.y, 0, Height - 1);

        if (IsWalkable(requested))
        {
            return requested;
        }

        int maxRadius = Mathf.Max(Width, Height);
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = requested.x - radius; x <= requested.x + radius; x++)
            {
                Vector2Int bottom = new Vector2Int(x, requested.y - radius);
                Vector2Int top = new Vector2Int(x, requested.y + radius);
                if (IsWalkable(bottom)) return bottom;
                if (IsWalkable(top)) return top;
            }

            for (int y = requested.y - radius + 1; y <= requested.y + radius - 1; y++)
            {
                Vector2Int left = new Vector2Int(requested.x - radius, y);
                Vector2Int right = new Vector2Int(requested.x + radius, y);
                if (IsWalkable(left)) return left;
                if (IsWalkable(right)) return right;
            }
        }

        return new Vector2Int(1, 1);
    }

    public List<Vector2> FindPathWorld(Vector2 startWorld, Vector2 goalWorld)
    {
        Vector2Int start = GetNearestWalkableCell(startWorld);
        Vector2Int goal = GetNearestWalkableCell(goalWorld);
        List<Vector2Int> cells = FindPathCells(start, goal);
        List<Vector2> worldPath = new List<Vector2>(cells.Count);

        // The first cell is normally the cell the character already occupies.
        for (int i = 1; i < cells.Count; i++)
        {
            worldPath.Add(CellToWorld(cells[i]));
        }

        return worldPath;
    }

    public List<Vector2Int> FindPathCells(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> empty = new List<Vector2Int>();
        if (!IsWalkable(start) || !IsWalkable(goal))
        {
            return empty;
        }

        if (start == goal)
        {
            return new List<Vector2Int> { start };
        }

        float[,] gScore = new float[Width, Height];
        bool[,] closed = new bool[Width, Height];
        bool[,] hasParent = new bool[Width, Height];
        Vector2Int[,] parent = new Vector2Int[Width, Height];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                gScore[x, y] = float.PositiveInfinity;
            }
        }

        List<Vector2Int> open = new List<Vector2Int> { start };
        bool[,] inOpen = new bool[Width, Height];
        inOpen[start.x, start.y] = true;
        gScore[start.x, start.y] = 0f;

        while (open.Count > 0)
        {
            int bestIndex = 0;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < open.Count; i++)
            {
                Vector2Int candidate = open[i];
                float score = gScore[candidate.x, candidate.y] + Manhattan(candidate, goal);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            Vector2Int current = open[bestIndex];
            open.RemoveAt(bestIndex);
            inOpen[current.x, current.y] = false;

            if (current == goal)
            {
                return ReconstructPath(parent, hasParent, current);
            }

            closed[current.x, current.y] = true;

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;
                if (!IsWalkable(next) || closed[next.x, next.y])
                {
                    continue;
                }

                float tentative = gScore[current.x, current.y] + 1f;
                if (tentative >= gScore[next.x, next.y])
                {
                    continue;
                }

                parent[next.x, next.y] = current;
                hasParent[next.x, next.y] = true;
                gScore[next.x, next.y] = tentative;

                if (!inOpen[next.x, next.y])
                {
                    open.Add(next);
                    inOpen[next.x, next.y] = true;
                }
            }
        }

        return empty;
    }

    public Vector2Int GetFarthestCell(Vector2Int start)
    {
        int[,] distance = CreateDistanceArray();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        distance[start.x, start.y] = 0;

        Vector2Int farthest = start;
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (distance[current.x, current.y] > distance[farthest.x, farthest.y])
            {
                farthest = current;
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;
                if (!IsWalkable(next) || distance[next.x, next.y] >= 0)
                {
                    continue;
                }

                distance[next.x, next.y] = distance[current.x, current.y] + 1;
                queue.Enqueue(next);
            }
        }

        return farthest;
    }

    public List<Vector2Int> GetWalkableCells()
    {
        List<Vector2Int> result = new List<Vector2Int>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsWalkable(cell))
                {
                    result.Add(cell);
                }
            }
        }

        return result;
    }

    public List<Vector2Int> GetDeadEnds()
    {
        List<Vector2Int> result = new List<Vector2Int>();
        foreach (Vector2Int cell in GetWalkableCells())
        {
            int neighbours = 0;
            foreach (Vector2Int direction in Directions)
            {
                if (IsWalkable(cell + direction))
                {
                    neighbours++;
                }
            }

            if (neighbours == 1)
            {
                result.Add(cell);
            }
        }

        return result;
    }

    public List<Vector2Int> GetWalkableCellsInRadius(Vector2Int center, int radius)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        int safeRadius = Mathf.Max(1, radius);

        for (int x = center.x - safeRadius; x <= center.x + safeRadius; x++)
        {
            for (int y = center.y - safeRadius; y <= center.y + safeRadius; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsWalkable(cell) && Manhattan(cell, center) <= safeRadius)
                {
                    result.Add(cell);
                }
            }
        }

        return result;
    }

    private int[,] CreateDistanceArray()
    {
        int[,] result = new int[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                result[x, y] = -1;
            }
        }

        return result;
    }

    private static float Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static List<Vector2Int> ReconstructPath(
        Vector2Int[,] parent,
        bool[,] hasParent,
        Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };

        while (hasParent[current.x, current.y])
        {
            current = parent[current.x, current.y];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
