using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a rectangular stealth maze with connected corridors, open rooms,
/// visual floor detail, brick collision walls, hiding spots, and pathfinding guards.
/// </summary>
[DisallowMultipleComponent]
public class MazeBuilder : MonoBehaviour
{
    public enum MazeLayoutType
    {
        ClassicLabyrinth,
        SecurityCompound,
        SpiralLockdown,
        TwinRoutes,
        ArenaMaze,
        RandomMix
    }

    public enum VisualTheme
    {
        SecurityFacility,
        LaboratoryComplex,
        NightIndustrial,
        MansionInterior,
        OvergrownRuins,
        StadiumVault
    }

    private enum GuardArchetype
    {
        Standard,
        Heavy,
        Scout,
        Elite
    }

    [Header("Maze structure")]
    [Min(11)] public int width = 43;
    [Min(11)] public int height = 31;
    [Min(0.5f)] public float cellSize = 1f;
    [Min(0)] public int randomSeed = 2026;
    public MazeLayoutType layoutType = MazeLayoutType.SecurityCompound;
    public VisualTheme visualTheme = VisualTheme.SecurityFacility;
    [Range(0, 120)] public int extraConnections = 34;
    [Range(0, 14)] public int roomCount = 7;
    [Range(3, 9)] public int roomMinSize = 3;
    [Range(3, 11)] public int roomMaxSize = 7;
    public bool buildOnAwake = true;

    [Header("Gameplay population")]
    [Range(1, 16)] public int guardCount = 8;
    [Range(2, 20)] public int hidingSpotCount = 11;
    [Range(2, 7)] public int patrolPointsPerGuard = 5;

    [Header("Character appearance")]
    public string characterResourcePath = "Sprites/PlayerPatrol";
    public string[] guardSpriteResourcePaths =
    {
        "Sprites/PatrolStandard",
        "Sprites/PatrolHeavy",
        "Sprites/PatrolScout",
        "Sprites/PatrolElite"
    };
    [Range(0.45f, 1.25f)] public float playerVisualHeight = 0.94f;
    [Range(0.35f, 1.15f)] public float guardVisualHeight = 0.80f;
    public Color guardBaseTint = Color.white;

    [Header("Movement and detection")]
    [Min(0.1f)] public float playerSpeed = 4.5f;
    [Min(0.1f)] public float guardPatrolSpeed = 2.1f;
    [Min(0.1f)] public float guardChaseSpeed = 3.45f;
    [Min(0.1f)] public float guardIdentificationTime = 1.65f;

    [Header("Visual detail")]
    public bool drawWallShadows = true;
    [Range(0f, 0.25f)] public float wallShadowOffset = 0.09f;
    [Range(0f, 0.4f)] public float floorColourVariation = 0.07f;
    [Range(0, 80)] public int ambientPropCount = 34;

    private const string GeneratedRootName = "__GeneratedMaze";

    private System.Random random;
    private bool[,] walkable;
    private MazeGrid grid;
    private Transform generatedRoot;

    private static Sprite solidSprite;
    private static Sprite brickSprite;
    private static Sprite floorTileSprite;
    private static Sprite fallbackCharacterSprite;
    private static Sprite characterSprite;
    private static Sprite[] guardSprites;
    private static Sprite backgroundSprite;
    private static Sprite hidingSprite;
    private static Sprite exitSprite;
    private static Sprite floorDetailSprite;
    private Sprite themeFloorSprite;
    private Sprite themeWallSprite;
    private Sprite themeDetailSprite;
    private Sprite themeBackgroundSprite;

    private void Awake()
    {
        if (buildOnAwake)
        {
            BuildCompleteGame();
        }
    }

    [ContextMenu("Build Complete Game")]
    public void BuildCompleteGame()
    {
        NormalizeDimensions();
        ClearGeneratedObjects();

        random = randomSeed == 0
            ? new System.Random(Environment.TickCount)
            : new System.Random(randomSeed);

        generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);

        walkable = GenerateMaze();
        Vector2 origin = new Vector2(
            -(width - 1) * cellSize * 0.5f,
            -(height - 1) * cellSize * 0.5f);

        GameObject gridObject = new GameObject("MazeGrid");
        gridObject.transform.SetParent(generatedRoot, false);
        grid = gridObject.AddComponent<MazeGrid>();
        grid.Initialize(walkable, cellSize, origin);

        EnsureGameManager();
        LoadThemeSprites();
        BuildBackdrop();
        BuildFloor();
        BuildWalls();
        BuildWallDecorations();

        Vector2Int startCell = new Vector2Int(1, 1);
        Vector2Int exitCell = grid.GetFarthestCell(startCell);

        PlayerController player = BuildPlayer(startCell);
        BuildExit(exitCell);
        BuildAmbientProps(startCell, exitCell);

        List<Vector2Int> hidingCells = BuildHidingSpots(startCell, exitCell);
        BuildGuards(startCell, exitCell, hidingCells);
        ConfigureCamera(player.transform);
    }

    private void NormalizeDimensions()
    {
        width = Mathf.Max(11, width);
        height = Mathf.Max(11, height);
        if (width % 2 == 0) width++;
        if (height % 2 == 0) height++;

        roomMinSize = MakeOdd(Mathf.Clamp(roomMinSize, 3, 9));
        roomMaxSize = MakeOdd(Mathf.Clamp(roomMaxSize, roomMinSize, 11));
        cellSize = Mathf.Max(0.5f, cellSize);
    }

    private bool[,] GenerateMaze()
    {
        MazeLayoutType selectedLayout = layoutType;
        if (selectedLayout == MazeLayoutType.RandomMix)
        {
            selectedLayout = (MazeLayoutType)random.Next(0, 5);
        }

        bool[,] map = GenerateClassicMaze();

        switch (selectedLayout)
        {
            case MazeLayoutType.SecurityCompound:
                CarveSecurityCompound(map);
                break;
            case MazeLayoutType.SpiralLockdown:
                CarveSpiralLockdown(map);
                break;
            case MazeLayoutType.TwinRoutes:
                CarveTwinRoutes(map);
                break;
            case MazeLayoutType.ArenaMaze:
                CarveArenaMaze(map);
                break;
        }

        AddExtraConnections(map, selectedLayout == MazeLayoutType.ClassicLabyrinth
            ? extraConnections
            : Mathf.RoundToInt(extraConnections * 0.65f));
        AddCornerCutouts(map);
        map[1, 1] = true;
        return map;
    }

    private bool[,] GenerateClassicMaze()
    {
        bool[,] map = new bool[width, height];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int start = new Vector2Int(1, 1);
        map[start.x, start.y] = true;
        stack.Push(start);

        Vector2Int[] twoStepDirections =
        {
            new Vector2Int(0, 2),
            new Vector2Int(2, 0),
            new Vector2Int(0, -2),
            new Vector2Int(-2, 0)
        };

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> possible = new List<Vector2Int>();

            foreach (Vector2Int direction in twoStepDirections)
            {
                Vector2Int next = current + direction;
                if (next.x > 0 && next.x < width - 1 &&
                    next.y > 0 && next.y < height - 1 &&
                    !map[next.x, next.y])
                {
                    possible.Add(direction);
                }
            }

            if (possible.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int selected = possible[random.Next(possible.Count)];
            Vector2Int between = current + new Vector2Int(selected.x / 2, selected.y / 2);
            Vector2Int destination = current + selected;
            map[between.x, between.y] = true;
            map[destination.x, destination.y] = true;
            stack.Push(destination);
        }

        CarveConnectedRooms(map);
        return map;
    }

    private void CarveSecurityCompound(bool[,] map)
    {
        int corridorY1 = Mathf.Clamp(height / 3, 2, height - 3);
        int corridorY2 = Mathf.Clamp((height * 2) / 3, 2, height - 3);
        CarveHorizontal(map, corridorY1, 1, width - 2, 2);
        CarveHorizontal(map, corridorY2, 1, width - 2, 2);

        int[] columns = { width / 5, width / 2, (width * 4) / 5 };
        foreach (int column in columns)
        {
            CarveVertical(map, column, 1, height - 2, 2);
        }

        int roomW = Mathf.Max(5, width / 7);
        int roomH = Mathf.Max(5, height / 6);
        CarveRoom(map, width / 4, height / 2, roomW, roomH);
        CarveRoom(map, (width * 3) / 4, height / 2, roomW, roomH);
        CarveRoom(map, width / 2, height / 5, roomW + 2, roomH);
        CarveRoom(map, width / 2, (height * 4) / 5, roomW + 2, roomH);
    }

    private void CarveSpiralLockdown(bool[,] map)
    {
        int left = 2;
        int right = width - 3;
        int bottom = 2;
        int top = height - 3;
        int gateIndex = 0;

        while (left < right && bottom < top)
        {
            CarveHorizontal(map, bottom, left, right, 1);
            CarveVertical(map, right, bottom, top, 1);
            CarveHorizontal(map, top, left + 2, right, 1);
            CarveVertical(map, left + 2, bottom + 2, top, 1);

            Vector2Int gate;
            switch (gateIndex % 4)
            {
                case 0:
                    gate = new Vector2Int(Mathf.Clamp(left + 4, 1, width - 2), bottom + 1);
                    break;
                case 1:
                    gate = new Vector2Int(right - 1, Mathf.Clamp(bottom + 4, 1, height - 2));
                    break;
                case 2:
                    gate = new Vector2Int(Mathf.Clamp(right - 4, 1, width - 2), top - 1);
                    break;
                default:
                    gate = new Vector2Int(left + 1, Mathf.Clamp(top - 4, 1, height - 2));
                    break;
            }
            map[gate.x, gate.y] = true;

            left += 4;
            right -= 4;
            bottom += 4;
            top -= 4;
            gateIndex++;
        }

        CarveRoom(map, width / 2, height / 2, 7, 7);
    }

    private void CarveTwinRoutes(bool[,] map)
    {
        int upper = Mathf.Clamp((height * 2) / 3, 2, height - 3);
        int lower = Mathf.Clamp(height / 3, 2, height - 3);
        CarveHorizontal(map, upper, 1, width - 2, 3);
        CarveHorizontal(map, lower, 1, width - 2, 3);

        int connectorCount = 5;
        for (int i = 0; i < connectorCount; i++)
        {
            int x = 3 + i * Mathf.Max(3, (width - 7) / (connectorCount - 1));
            CarveVertical(map, Mathf.Clamp(x, 2, width - 3), lower, upper, i % 2 == 0 ? 2 : 1);
        }

        CarveRoom(map, width / 4, height / 2, 7, 7);
        CarveRoom(map, (width * 3) / 4, height / 2, 7, 7);
    }

    private void CarveArenaMaze(bool[,] map)
    {
        int arenaWidth = Mathf.Max(7, width / 5);
        int arenaHeight = Mathf.Max(7, height / 4);
        Vector2Int[] centers =
        {
            new Vector2Int(width / 4, height / 4),
            new Vector2Int((width * 3) / 4, height / 4),
            new Vector2Int(width / 4, (height * 3) / 4),
            new Vector2Int((width * 3) / 4, (height * 3) / 4),
            new Vector2Int(width / 2, height / 2)
        };

        foreach (Vector2Int center in centers)
        {
            CarveRoom(map, center.x, center.y, arenaWidth, arenaHeight);
        }

        CarveHorizontal(map, height / 2, 1, width - 2, 2);
        CarveVertical(map, width / 2, 1, height - 2, 2);
        CarveHorizontal(map, height / 4, width / 4, (width * 3) / 4, 1);
        CarveHorizontal(map, (height * 3) / 4, width / 4, (width * 3) / 4, 1);
    }

    private void CarveRoom(bool[,] map, int centerX, int centerY, int requestedWidth, int requestedHeight)
    {
        int roomWidth = Mathf.Clamp(MakeOdd(requestedWidth), 3, width - 2);
        int roomHeight = Mathf.Clamp(MakeOdd(requestedHeight), 3, height - 2);
        int left = Mathf.Clamp(centerX - roomWidth / 2, 1, width - roomWidth - 1);
        int bottom = Mathf.Clamp(centerY - roomHeight / 2, 1, height - roomHeight - 1);

        for (int x = left; x < left + roomWidth; x++)
        {
            for (int y = bottom; y < bottom + roomHeight; y++)
            {
                map[x, y] = true;
            }
        }
    }

    private void CarveHorizontal(bool[,] map, int y, int fromX, int toX, int thickness)
    {
        for (int offset = -(thickness / 2); offset <= thickness / 2; offset++)
        {
            int row = Mathf.Clamp(y + offset, 1, height - 2);
            for (int x = Mathf.Max(1, fromX); x <= Mathf.Min(width - 2, toX); x++)
            {
                map[x, row] = true;
            }
        }
    }

    private void CarveVertical(bool[,] map, int x, int fromY, int toY, int thickness)
    {
        for (int offset = -(thickness / 2); offset <= thickness / 2; offset++)
        {
            int column = Mathf.Clamp(x + offset, 1, width - 2);
            for (int y = Mathf.Max(1, fromY); y <= Mathf.Min(height - 2, toY); y++)
            {
                map[column, y] = true;
            }
        }
    }

    private void CarveConnectedRooms(bool[,] map)
    {
        for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
        {
            List<Vector2Int> existingCorridors = GetWalkableCells(map);
            if (existingCorridors.Count == 0)
            {
                return;
            }

            Vector2Int anchor = existingCorridors[random.Next(existingCorridors.Count)];
            int roomWidth = RandomOdd(roomMinSize, roomMaxSize);
            int roomHeight = RandomOdd(roomMinSize, roomMaxSize);

            int left = Mathf.Clamp(anchor.x - roomWidth / 2, 1, width - roomWidth - 1);
            int bottom = Mathf.Clamp(anchor.y - roomHeight / 2, 1, height - roomHeight - 1);

            for (int x = left; x < left + roomWidth; x++)
            {
                for (int y = bottom; y < bottom + roomHeight; y++)
                {
                    map[x, y] = true;
                }
            }
        }
    }

    private void AddExtraConnections(bool[,] map, int connectionCount)
    {
        List<Vector2Int> removableWalls = new List<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (map[x, y])
                {
                    continue;
                }

                bool horizontalConnection = map[x - 1, y] && map[x + 1, y];
                bool verticalConnection = map[x, y - 1] && map[x, y + 1];
                if (horizontalConnection ^ verticalConnection)
                {
                    removableWalls.Add(new Vector2Int(x, y));
                }
            }
        }

        Shuffle(removableWalls);
        int amount = Mathf.Min(connectionCount, removableWalls.Count);
        for (int i = 0; i < amount; i++)
        {
            Vector2Int cell = removableWalls[i];
            map[cell.x, cell.y] = true;
        }
    }

    private void AddCornerCutouts(bool[,] map)
    {
        int attempts = Mathf.Max(4, (width * height) / 180);
        for (int i = 0; i < attempts; i++)
        {
            int x = random.Next(2, width - 2);
            int y = random.Next(2, height - 2);
            if (!map[x, y])
            {
                continue;
            }

            Vector2Int[] neighbours =
            {
                new Vector2Int(x + 1, y),
                new Vector2Int(x - 1, y),
                new Vector2Int(x, y + 1),
                new Vector2Int(x, y - 1)
            };

            Vector2Int selected = neighbours[random.Next(neighbours.Length)];
            map[selected.x, selected.y] = true;
        }
    }

    private void BuildBackdrop()
    {
        GameObject backdrop = new GameObject("AmbientBackground");
        backdrop.transform.SetParent(generatedRoot, false);
        backdrop.transform.position = grid.WorldBounds.center;

        SpriteRenderer renderer = backdrop.AddComponent<SpriteRenderer>();
        renderer.sprite = GetBackgroundSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = -50;

        if (renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            float scaleX = (width * cellSize + 8f) / Mathf.Max(0.01f, spriteSize.x);
            float scaleY = (height * cellSize + 8f) / Mathf.Max(0.01f, spriteSize.y);
            backdrop.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            renderer.sprite = GetSolidSprite();
            renderer.color = GetBackdropFallbackColour();
            backdrop.transform.localScale = new Vector3(width * cellSize + 8f, height * cellSize + 8f, 1f);
        }
    }

    private void BuildFloor()
    {
        GameObject baseFloor = new GameObject("FloorBase");
        baseFloor.transform.SetParent(generatedRoot, false);
        baseFloor.transform.position = grid.WorldBounds.center;
        baseFloor.transform.localScale = new Vector3(width * cellSize, height * cellSize, 1f);

        SpriteRenderer baseRenderer = baseFloor.AddComponent<SpriteRenderer>();
        baseRenderer.sprite = GetSolidSprite();
        baseRenderer.color = GetFloorOverlayColour();
        baseRenderer.sortingOrder = -30;

        Transform tileRoot = new GameObject("DetailedFloorTiles").transform;
        tileRoot.SetParent(generatedRoot, false);
        Transform detailRoot = new GameObject("FloorDetails").transform;
        detailRoot.SetParent(generatedRoot, false);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!walkable[x, y])
                {
                    continue;
                }

                Vector2Int cell = new Vector2Int(x, y);
                GameObject tile = new GameObject($"Floor_{x}_{y}");
                tile.transform.SetParent(tileRoot, false);
                tile.transform.position = grid.CellToWorld(cell);
                tile.transform.localScale = Vector3.one * (cellSize * 0.985f);

                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = GetFloorTileSprite();
                renderer.color = GetFloorColour(x, y);
                renderer.sortingOrder = -12;

                if (ShouldPlaceFloorDetail(x, y))
                {
                    GameObject detail = new GameObject($"Detail_{x}_{y}");
                    detail.transform.SetParent(detailRoot, false);
                    detail.transform.position = grid.CellToWorld(cell);
                    detail.transform.rotation = Quaternion.Euler(0f, 0f, Hash01(x, y, 91) * 360f);
                    detail.transform.localScale = Vector3.one * (cellSize * (0.28f + Hash01(x, y, 73) * 0.22f));

                    SpriteRenderer detailRenderer = detail.AddComponent<SpriteRenderer>();
                    detailRenderer.sprite = GetFloorDetailSprite();
                    detailRenderer.color = GetFloorDetailColour(x, y);
                    detailRenderer.sortingOrder = -10;
                }
            }
        }
    }

    private void BuildWalls()
    {
        Transform wallRoot = new GameObject("BrickWalls").transform;
        wallRoot.SetParent(generatedRoot, false);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (walkable[x, y])
                {
                    continue;
                }

                Vector2Int cell = new Vector2Int(x, y);
                GameObject wall = new GameObject($"Wall_{x}_{y}");
                wall.transform.SetParent(wallRoot, false);
                wall.transform.position = grid.CellToWorld(cell);
                wall.transform.localScale = Vector3.one * cellSize;

                if (drawWallShadows)
                {
                    GameObject shadow = new GameObject("Shadow");
                    shadow.transform.SetParent(wall.transform, false);
                    shadow.transform.localPosition = new Vector3(wallShadowOffset, -wallShadowOffset, 0f);

                    SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
                    shadowRenderer.sprite = GetBrickSprite();
                    shadowRenderer.color = new Color(0f, 0f, 0f, 0.48f);
                    shadowRenderer.sortingOrder = -2;
                }

                SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
                renderer.sprite = GetBrickSprite();
                renderer.color = GetWallColour(x, y);
                renderer.sortingOrder = 0;

                if (IsWallEdgeCell(x, y) && Hash01(x, y, 131) > 0.73f)
                {
                    GameObject lightStrip = new GameObject("NeonEdge");
                    lightStrip.transform.SetParent(wall.transform, false);
                    lightStrip.transform.localPosition = new Vector3(0f, -0.38f, 0f);
                    lightStrip.transform.localScale = new Vector3(0.76f, 0.07f, 1f);
                    SpriteRenderer stripRenderer = lightStrip.AddComponent<SpriteRenderer>();
                    stripRenderer.sprite = GetSolidSprite();
                    stripRenderer.color = GetAccentColour(x, y);
                    stripRenderer.sortingOrder = 2;
                }

                BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one * 0.98f;
            }
        }
    }

    private void BuildWallDecorations()
    {
        Transform root = new GameObject("WallDecorations").transform;
        root.SetParent(generatedRoot, false);

        string[] propPaths =
        {
            "Props/SecurityCamera",
            "Props/WarningLight",
            "Props/Barrier"
        };

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (walkable[x, y] || !IsWallEdgeCell(x, y) || Hash01(x, y, 211) < 0.91f)
                {
                    continue;
                }

                int index = Mathf.FloorToInt(Hash01(x, y, 223) * propPaths.Length);
                index = Mathf.Clamp(index, 0, propPaths.Length - 1);
                Sprite sprite = Resources.Load<Sprite>(propPaths[index]);
                if (sprite == null)
                {
                    continue;
                }

                GameObject decoration = new GameObject($"Decoration_{x}_{y}");
                decoration.transform.SetParent(root, false);
                decoration.transform.position = grid.CellToWorld(new Vector2Int(x, y)) + new Vector2(0f, -cellSize * 0.08f);

                SpriteRenderer renderer = decoration.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                renderer.sortingOrder = 4;

                float desiredHeight = index == 2 ? cellSize * 0.48f : cellSize * 0.42f;
                float spriteHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
                decoration.transform.localScale = Vector3.one * (desiredHeight / spriteHeight);
            }
        }
    }

    private PlayerController BuildPlayer(Vector2Int cell)
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.transform.SetParent(generatedRoot, false);
        playerObject.transform.position = grid.CellToWorld(cell);

        CreateCharacterVisual(
            playerObject.transform,
            "PlayerVisual",
            playerVisualHeight * cellSize,
            Color.white,
            20);

        Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D collider = playerObject.AddComponent<CircleCollider2D>();
        collider.radius = cellSize * 0.27f;

        PlayerController controller = playerObject.AddComponent<PlayerController>();
        controller.moveSpeed = playerSpeed;
        return controller;
    }

    private void BuildExit(Vector2Int cell)
    {
        GameObject exit = new GameObject("Exit");
        exit.transform.SetParent(generatedRoot, false);
        exit.transform.position = grid.CellToWorld(cell);
        exit.transform.localScale = Vector3.one * (cellSize * 0.82f);

        SpriteRenderer renderer = exit.AddComponent<SpriteRenderer>();
        renderer.sprite = GetExitSprite();
        renderer.sortingOrder = 5;

        CircleCollider2D collider = exit.AddComponent<CircleCollider2D>();
        collider.radius = 0.48f;
        collider.isTrigger = true;
        exit.AddComponent<ExitTrigger>();
    }

    private List<Vector2Int> BuildHidingSpots(Vector2Int startCell, Vector2Int exitCell)
    {
        List<Vector2Int> candidates = grid.GetDeadEnds();
        candidates.RemoveAll(cell =>
            Manhattan(cell, startCell) < 5 ||
            Manhattan(cell, exitCell) < 2);

        if (candidates.Count < hidingSpotCount)
        {
            foreach (Vector2Int cell in grid.GetWalkableCells())
            {
                if (!candidates.Contains(cell) &&
                    Manhattan(cell, startCell) >= 5 &&
                    Manhattan(cell, exitCell) >= 2)
                {
                    candidates.Add(cell);
                }
            }
        }

        Shuffle(candidates);
        int amount = Mathf.Min(hidingSpotCount, candidates.Count);
        List<Vector2Int> selected = new List<Vector2Int>(amount);

        Transform root = new GameObject("HidingSpots").transform;
        root.SetParent(generatedRoot, false);

        for (int i = 0; i < amount; i++)
        {
            Vector2Int cell = candidates[i];
            selected.Add(cell);

            GameObject hidingSpot = new GameObject($"HidingSpot_{i + 1}");
            hidingSpot.transform.SetParent(root, false);
            hidingSpot.transform.position = grid.CellToWorld(cell);
            hidingSpot.transform.localScale = Vector3.one * (cellSize * 0.78f);

            SpriteRenderer renderer = hidingSpot.AddComponent<SpriteRenderer>();
            renderer.sprite = GetHidingSprite();
            renderer.sortingOrder = 8;

            BoxCollider2D collider = hidingSpot.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = true;
            hidingSpot.AddComponent<HidingSpot>();
        }

        return selected;
    }

    private void BuildGuards(
        Vector2Int startCell,
        Vector2Int exitCell,
        List<Vector2Int> hidingCells)
    {
        List<Vector2Int> candidates = grid.GetWalkableCells();
        candidates.RemoveAll(cell =>
            Manhattan(cell, startCell) < 9 ||
            Manhattan(cell, exitCell) < 2 ||
            hidingCells.Contains(cell));

        Shuffle(candidates);
        List<Vector2Int> spawnCells = SelectSeparatedGuardSpawns(candidates);
        int amount = Mathf.Min(guardCount, spawnCells.Count);

        Transform guardRoot = new GameObject("Guards").transform;
        guardRoot.SetParent(generatedRoot, false);
        Transform routeRoot = new GameObject("PatrolRoutes").transform;
        routeRoot.SetParent(generatedRoot, false);

        for (int i = 0; i < amount; i++)
        {
            Vector2Int spawnCell = spawnCells[i];
            Transform[] route = BuildPatrolRoute(i, spawnCell, candidates, routeRoot);

            GuardArchetype archetype = (GuardArchetype)(i % 4);
            GameObject guardObject = new GameObject($"{archetype}Patrol_{i + 1}");
            guardObject.transform.SetParent(guardRoot, false);
            guardObject.transform.position = grid.CellToWorld(spawnCell);

            float archetypeScale = archetype == GuardArchetype.Heavy
                ? 1.10f
                : archetype == GuardArchetype.Scout
                    ? 0.94f
                    : archetype == GuardArchetype.Elite
                        ? 1.04f
                        : 1f;

            CreateCharacterVisual(
                guardObject.transform,
                "GuardVisual",
                guardVisualHeight * archetypeScale * cellSize,
                guardBaseTint,
                18,
                GetGuardSprite(archetype));

            Rigidbody2D body = guardObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.mass = 50f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = guardObject.AddComponent<CircleCollider2D>();
            collider.radius = cellSize * (archetype == GuardArchetype.Heavy ? 0.27f : 0.24f);

            GuardAI guard = guardObject.AddComponent<GuardAI>();
            guard.patrolPoints = route;
            ConfigureGuardArchetype(guard, archetype);

            GuardVisionCone cone = guardObject.AddComponent<GuardVisionCone>();
            cone.guard = guard;
            cone.sortingOrder = 12;
            ConfigureVisionCone(cone, archetype);
        }
    }

    private List<Vector2Int> SelectSeparatedGuardSpawns(List<Vector2Int> candidates)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        foreach (Vector2Int candidate in candidates)
        {
            bool separated = true;
            foreach (Vector2Int chosen in result)
            {
                if (Manhattan(candidate, chosen) < 6)
                {
                    separated = false;
                    break;
                }
            }

            if (!separated)
            {
                continue;
            }

            result.Add(candidate);
            if (result.Count >= guardCount)
            {
                break;
            }
        }

        if (result.Count < guardCount)
        {
            foreach (Vector2Int candidate in candidates)
            {
                if (!result.Contains(candidate))
                {
                    result.Add(candidate);
                }

                if (result.Count >= guardCount)
                {
                    break;
                }
            }
        }

        return result;
    }

    private Transform[] BuildPatrolRoute(
        int guardIndex,
        Vector2Int startCell,
        List<Vector2Int> candidates,
        Transform routeRoot)
    {
        int count = Mathf.Max(2, patrolPointsPerGuard);
        Transform[] points = new Transform[count];
        Vector2Int previous = startCell;
        List<Vector2Int> used = new List<Vector2Int> { startCell };

        for (int p = 0; p < count; p++)
        {
            Vector2Int selected = p == 0
                ? startCell
                : SelectRegionalPatrolCell(previous, startCell, candidates, used);

            GameObject point = new GameObject($"Guard_{guardIndex + 1}_Point_{p + 1}");
            point.transform.SetParent(routeRoot, false);
            point.transform.position = grid.CellToWorld(selected);
            points[p] = point.transform;
            used.Add(selected);
            previous = selected;
        }

        return points;
    }

    private Vector2Int SelectRegionalPatrolCell(
        Vector2Int from,
        Vector2Int routeOrigin,
        List<Vector2Int> candidates,
        List<Vector2Int> used)
    {
        List<Vector2Int> regional = candidates.FindAll(cell =>
            !used.Contains(cell) &&
            Manhattan(cell, from) >= 4 &&
            Manhattan(cell, from) <= 13 &&
            Manhattan(cell, routeOrigin) <= 18);

        if (regional.Count == 0)
        {
            regional = candidates.FindAll(cell => !used.Contains(cell));
        }

        if (regional.Count == 0)
        {
            return from;
        }

        Vector2Int best = regional[random.Next(regional.Count)];
        int bestDistance = Manhattan(from, best);
        int samples = Mathf.Min(32, regional.Count);
        for (int i = 0; i < samples; i++)
        {
            Vector2Int candidate = regional[random.Next(regional.Count)];
            int distance = Manhattan(from, candidate);
            if (distance > bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private SpriteRenderer CreateCharacterVisual(
        Transform parent,
        string objectName,
        float desiredWorldHeight,
        Color tint,
        int sortingOrder,
        Sprite sourceSprite = null)
    {
        GameObject visual = new GameObject(objectName);
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sourceSprite != null ? sourceSprite : GetCharacterSprite();
        renderer.color = tint;
        renderer.sortingOrder = sortingOrder;

        float spriteHeight = Mathf.Max(0.001f, renderer.sprite.bounds.size.y);
        float visualScale = desiredWorldHeight / spriteHeight;
        visual.transform.localScale = Vector3.one * visualScale;
        return renderer;
    }

    private void ConfigureGuardArchetype(GuardAI guard, GuardArchetype archetype)
    {
        float randomSpeed = (float)random.NextDouble() * 0.18f;
        float randomVision = (float)random.NextDouble() * 0.45f;

        switch (archetype)
        {
            case GuardArchetype.Heavy:
                guard.patrolSpeed = guardPatrolSpeed * 0.82f + randomSpeed;
                guard.chaseSpeed = guardChaseSpeed * 0.92f + randomSpeed;
                guard.viewDistance = 6.5f + randomVision;
                guard.viewAngle = 92f;
                guard.identificationTime = guardIdentificationTime * 0.82f;
                guard.searchDuration = 10f;
                guard.searchRadius = 7;
                break;
            case GuardArchetype.Scout:
                guard.patrolSpeed = guardPatrolSpeed * 1.20f + randomSpeed;
                guard.chaseSpeed = guardChaseSpeed * 1.22f + randomSpeed;
                guard.viewDistance = 7.6f + randomVision;
                guard.viewAngle = 66f;
                guard.identificationTime = guardIdentificationTime * 1.08f;
                guard.searchDuration = 6.5f;
                guard.searchRadius = 4;
                break;
            case GuardArchetype.Elite:
                guard.patrolSpeed = guardPatrolSpeed * 1.06f + randomSpeed;
                guard.chaseSpeed = guardChaseSpeed * 1.12f + randomSpeed;
                guard.viewDistance = 7.2f + randomVision;
                guard.viewAngle = 86f;
                guard.identificationTime = guardIdentificationTime * 0.68f;
                guard.searchDuration = 11f;
                guard.searchRadius = 8;
                guard.identificationCooldown = 1.7f;
                break;
            default:
                guard.patrolSpeed = guardPatrolSpeed + randomSpeed;
                guard.chaseSpeed = guardChaseSpeed + randomSpeed;
                guard.viewDistance = 6.2f + randomVision;
                guard.viewAngle = 78f;
                guard.identificationTime = guardIdentificationTime;
                guard.searchDuration = 8f;
                guard.searchRadius = 5;
                break;
        }
    }

    private static void ConfigureVisionCone(GuardVisionCone cone, GuardArchetype archetype)
    {
        switch (archetype)
        {
            case GuardArchetype.Heavy:
                cone.patrolColour = new Color(1f, 0.18f, 0.03f, 0.25f);
                cone.alertColour = new Color(1f, 0.38f, 0.03f, 0.34f);
                cone.chaseColour = new Color(1f, 0.03f, 0.02f, 0.46f);
                break;
            case GuardArchetype.Scout:
                cone.patrolColour = new Color(1f, 0.05f, 0.22f, 0.21f);
                cone.alertColour = new Color(1f, 0.24f, 0.08f, 0.31f);
                cone.chaseColour = new Color(1f, 0.02f, 0.12f, 0.43f);
                break;
            case GuardArchetype.Elite:
                cone.patrolColour = new Color(0.92f, 0.04f, 0.16f, 0.27f);
                cone.alertColour = new Color(1f, 0.28f, 0.04f, 0.36f);
                cone.chaseColour = new Color(1f, 0.01f, 0.05f, 0.49f);
                break;
            default:
                cone.patrolColour = new Color(1f, 0.06f, 0.08f, 0.23f);
                cone.alertColour = new Color(1f, 0.32f, 0.04f, 0.33f);
                cone.chaseColour = new Color(1f, 0.02f, 0.04f, 0.44f);
                break;
        }
    }

    private Sprite GetGuardSprite(GuardArchetype archetype)
    {
        int index = (int)archetype;
        if (guardSprites == null || guardSprites.Length != 4)
        {
            guardSprites = new Sprite[4];
        }

        if (guardSprites[index] == null && guardSpriteResourcePaths != null && index < guardSpriteResourcePaths.Length)
        {
            guardSprites[index] = Resources.Load<Sprite>(guardSpriteResourcePaths[index]);
        }

        return guardSprites[index] != null ? guardSprites[index] : GetCharacterSprite();
    }

    private Sprite GetBackgroundSprite()
    {
        if (themeBackgroundSprite == null)
        {
            themeBackgroundSprite = Resources.Load<Sprite>($"Themes/{visualTheme}/Background");
        }

        return themeBackgroundSprite;
    }

    private void ConfigureCamera(Transform player)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 5.35f;
        camera.backgroundColor = GetCameraBackgroundColour();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(player.position.x, player.position.y, -10f);

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<CameraFollow2D>();
        }

        follow.target = player;
        follow.normalZoom = 5.35f;
        follow.dangerZoom = 4.80f;
        follow.smoothTime = 0.10f;

        if (camera.GetComponent<StealthScreenOverlay>() == null)
        {
            camera.gameObject.AddComponent<StealthScreenOverlay>();
        }
    }

    private void EnsureGameManager()
    {
        if (FindFirstObjectByType<GameManager>() == null)
        {
            new GameObject("GameManager").AddComponent<GameManager>();
        }
    }

    private void ClearGeneratedObjects()
    {
        Transform oldRoot = transform.Find(GeneratedRootName);
        if (oldRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(oldRoot.gameObject);
        }
        else
        {
            DestroyImmediate(oldRoot.gameObject);
        }
    }

    private Color GetFloorColour(int x, int y)
    {
        float variation = (Hash01(x, y, 17) - 0.5f) * floorColourVariation;
        Color baseColour;
        switch (visualTheme)
        {
            case VisualTheme.LaboratoryComplex:
                baseColour = new Color(0.76f, 0.98f, 0.91f, 1f);
                break;
            case VisualTheme.NightIndustrial:
                baseColour = new Color(1f, 0.90f, 0.78f, 1f);
                break;
            case VisualTheme.MansionInterior:
                baseColour = new Color(1f, 0.82f, 0.78f, 1f);
                break;
            case VisualTheme.OvergrownRuins:
                baseColour = new Color(0.86f, 1f, 0.82f, 1f);
                break;
            default:
                baseColour = new Color(0.82f, 0.94f, 1f, 1f);
                break;
        }

        return new Color(
            Mathf.Clamp01(baseColour.r + variation),
            Mathf.Clamp01(baseColour.g + variation),
            Mathf.Clamp01(baseColour.b + variation),
            1f);
    }

    private Color GetWallColour(int x, int y)
    {
        float tint = (Hash01(x, y, 37) - 0.5f) * 0.10f;
        Color baseColour;
        switch (visualTheme)
        {
            case VisualTheme.LaboratoryComplex:
                baseColour = new Color(0.80f, 1f, 0.92f);
                break;
            case VisualTheme.NightIndustrial:
                baseColour = new Color(1f, 0.83f, 0.68f);
                break;
            case VisualTheme.MansionInterior:
                baseColour = new Color(1f, 0.76f, 0.69f);
                break;
            case VisualTheme.OvergrownRuins:
                baseColour = new Color(0.82f, 0.96f, 0.76f);
                break;
            default:
                baseColour = new Color(0.78f, 0.91f, 1f);
                break;
        }

        return new Color(
            Mathf.Clamp01(baseColour.r + tint),
            Mathf.Clamp01(baseColour.g + tint),
            Mathf.Clamp01(baseColour.b + tint),
            1f);
    }

    private Color GetFloorOverlayColour()
    {
        switch (visualTheme)
        {
            case VisualTheme.LaboratoryComplex:
                return new Color(0.01f, 0.08f, 0.07f, 0.42f);
            case VisualTheme.NightIndustrial:
                return new Color(0.10f, 0.05f, 0.025f, 0.48f);
            case VisualTheme.MansionInterior:
                return new Color(0.10f, 0.018f, 0.03f, 0.48f);
            case VisualTheme.OvergrownRuins:
                return new Color(0.025f, 0.075f, 0.035f, 0.44f);
            default:
                return new Color(0.012f, 0.045f, 0.085f, 0.46f);
        }
    }

    private Color GetFloorDetailColour(int x, int y)
    {
        Color accent = GetAccentColour(x, y);
        accent.a = 0.44f;
        return accent;
    }

    private Color GetAccentColour(int x, int y)
    {
        bool alternate = Hash01(x, y, 143) > 0.55f;
        switch (visualTheme)
        {
            case VisualTheme.LaboratoryComplex:
                return alternate
                    ? new Color(0.16f, 1f, 0.72f, 0.95f)
                    : new Color(0.18f, 0.76f, 1f, 0.95f);
            case VisualTheme.NightIndustrial:
                return alternate
                    ? new Color(1f, 0.68f, 0.08f, 0.95f)
                    : new Color(1f, 0.20f, 0.06f, 0.95f);
            case VisualTheme.MansionInterior:
                return alternate
                    ? new Color(1f, 0.72f, 0.18f, 0.95f)
                    : new Color(0.92f, 0.12f, 0.22f, 0.95f);
            case VisualTheme.OvergrownRuins:
                return alternate
                    ? new Color(0.46f, 0.88f, 0.30f, 0.95f)
                    : new Color(0.12f, 0.72f, 0.46f, 0.95f);
            default:
                return alternate
                    ? new Color(0.08f, 0.88f, 1f, 0.95f)
                    : new Color(0.30f, 0.46f, 1f, 0.95f);
        }
    }

    private bool IsWallEdgeCell(int x, int y)
    {
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        foreach (Vector2Int direction in directions)
        {
            int nx = x + direction.x;
            int ny = y + direction.y;
            if (nx >= 0 && nx < width && ny >= 0 && ny < height && walkable[nx, ny])
            {
                return true;
            }
        }

        return false;
    }

    private Color GetBackdropFallbackColour()
    {
        switch (visualTheme)
        {
            case VisualTheme.LaboratoryComplex:
                return new Color(0.004f, 0.035f, 0.03f);
            case VisualTheme.NightIndustrial:
                return new Color(0.055f, 0.025f, 0.018f);
            case VisualTheme.MansionInterior:
                return new Color(0.055f, 0.012f, 0.022f);
            case VisualTheme.OvergrownRuins:
                return new Color(0.012f, 0.04f, 0.018f);
            default:
                return new Color(0.006f, 0.025f, 0.055f);
        }
    }

    private Color GetCameraBackgroundColour()
    {
        return GetBackdropFallbackColour();
    }

    private bool ShouldPlaceFloorDetail(int x, int y)
    {
        return Hash01(x, y, 59) > 0.86f;
    }

    private static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
            hash = (hash << 13) ^ hash;
            int value = hash * (hash * hash * 15731 + 789221) + 1376312589;
            int positive = value & 0x7fffffff;
            return (positive % 10000) / 9999f;
        }
    }

    private List<Vector2Int> GetWalkableCells(bool[,] map)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (map[x, y])
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
        }

        return cells;
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private int RandomOdd(int minimum, int maximum)
    {
        int value = random.Next(minimum, maximum + 1);
        return MakeOdd(value);
    }

    private static int MakeOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void LoadThemeSprites()
    {
        string root = $"Themes/{visualTheme}";
        themeFloorSprite = Resources.Load<Sprite>($"{root}/Floor");
        themeWallSprite = Resources.Load<Sprite>($"{root}/Wall");
        themeDetailSprite = Resources.Load<Sprite>($"{root}/Detail");
        themeBackgroundSprite = Resources.Load<Sprite>($"{root}/Background");
    }

    private void BuildAmbientProps(Vector2Int startCell, Vector2Int exitCell)
    {
        List<Vector2Int> candidates = grid.GetWalkableCells();
        candidates.RemoveAll(cell =>
            Manhattan(cell, startCell) < 4 ||
            Manhattan(cell, exitCell) < 3 ||
            CountWalkableNeighbours(cell) > 3);
        Shuffle(candidates);

        string[] paths = GetThemePropPaths();
        int amount = Mathf.Min(ambientPropCount, candidates.Count);
        Transform root = new GameObject("AmbientProps").transform;
        root.SetParent(generatedRoot, false);

        for (int i = 0; i < amount; i++)
        {
            Vector2Int cell = candidates[i];
            Sprite sprite = Resources.Load<Sprite>(paths[i % paths.Length]);
            if (sprite == null)
            {
                continue;
            }

            GameObject prop = new GameObject($"Prop_{i + 1}");
            prop.transform.SetParent(root, false);
            Vector2 offset = new Vector2(
                (Hash01(cell.x, cell.y, 401) - 0.5f) * cellSize * 0.42f,
                (Hash01(cell.x, cell.y, 409) - 0.5f) * cellSize * 0.42f);
            prop.transform.position = grid.CellToWorld(cell) + offset;
            prop.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(Hash01(cell.x, cell.y, 419) * 3f) * 90f);

            SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 7;

            float desiredHeight = cellSize * (0.34f + Hash01(cell.x, cell.y, 431) * 0.18f);
            float spriteHeight = Mathf.Max(0.001f, sprite.bounds.size.y);
            prop.transform.localScale = Vector3.one * (desiredHeight / spriteHeight);
        }
    }

    private string[] GetThemePropPaths()
    {
        switch (visualTheme)
        {
            case VisualTheme.LaboratoryComplex:
                return new[] { "Props/HidingLocker", "Props/SecurityCamera", "Props/Vent", "Props/WarningLight" };
            case VisualTheme.NightIndustrial:
                return new[] { "Props/Crate", "Props/Barrel", "Props/Barrier", "Props/WarningLight" };
            case VisualTheme.MansionInterior:
                return new[] { "Props/Desk", "Props/Plant", "Props/Crate", "Props/WarningLight" };
            case VisualTheme.OvergrownRuins:
                return new[] { "Props/Plant", "Props/Crate", "Props/Barrel", "Props/Vent" };
            default:
                return new[] { "Props/Crate", "Props/SecurityCamera", "Props/Vent", "Props/HidingLocker" };
        }
    }

    private int CountWalkableNeighbours(Vector2Int cell)
    {
        int count = 0;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (Vector2Int direction in directions)
        {
            if (grid.IsWalkable(cell + direction))
            {
                count++;
            }
        }
        return count;
    }

    private Sprite GetCharacterSprite()
    {
        if (characterSprite == null)
        {
            characterSprite = Resources.Load<Sprite>(characterResourcePath);
        }

        return characterSprite != null ? characterSprite : GetFallbackCharacterSprite();
    }

    private static Sprite GetSolidSprite()
    {
        if (solidSprite == null)
        {
            solidSprite = CreateSprite("Solid", 8, (x, y) => Color.white);
        }

        return solidSprite;
    }

    private Sprite GetFloorTileSprite()
    {
        return themeFloorSprite != null ? themeFloorSprite : GetSolidSprite();
    }

    private Sprite GetFloorDetailSprite()
    {
        return themeDetailSprite != null ? themeDetailSprite : GetSolidSprite();
    }

    private Sprite GetBrickSprite()
    {
        return themeWallSprite != null ? themeWallSprite : GetSolidSprite();
    }

    private static Sprite GetFallbackCharacterSprite()
    {
        if (fallbackCharacterSprite == null)
        {
            fallbackCharacterSprite = CreateSprite("FallbackCharacter", 64, (x, y) =>
            {
                Vector2 point = new Vector2(x - 31.5f, y - 31.5f);
                float distance = point.magnitude;
                if (distance > 29f) return Color.clear;
                if (distance > 24f) return new Color(0.05f, 0.25f, 0.32f);
                return new Color(0.2f, 0.9f, 1f);
            });
        }

        return fallbackCharacterSprite;
    }

    private static Sprite GetHidingSprite()
    {
        if (hidingSprite == null)
        {
            hidingSprite = Resources.Load<Sprite>("Props/HidingLocker");
        }

        if (hidingSprite == null)
        {
            hidingSprite = CreateSprite("HidingCrate", 64, (x, y) =>
            {
                bool border = x < 5 || x > 58 || y < 5 || y > 58;
                bool plank = Mathf.Abs(x - y) < 4 || Mathf.Abs((63 - x) - y) < 4;
                if (border) return new Color(0.07f, 0.18f, 0.11f);
                if (plank) return new Color(0.18f, 0.46f, 0.24f);
                return new Color(0.11f, 0.32f, 0.17f);
            });
        }

        return hidingSprite;
    }

    private static Sprite GetExitSprite()
    {
        if (exitSprite == null)
        {
            exitSprite = Resources.Load<Sprite>("Props/ExitPortal");
        }

        if (exitSprite == null)
        {
            exitSprite = CreateSprite("Exit", 64, (x, y) =>
            {
                bool border = x < 5 || x > 58 || y < 5 || y > 58;
                bool stripe = ((x + y) / 10) % 2 == 0;
                if (border) return new Color(0.04f, 0.24f, 0.07f);
                return stripe ? new Color(0.3f, 1f, 0.43f) : new Color(0.08f, 0.68f, 0.2f);
            });
        }

        return exitSprite;
    }

    private static Sprite CreateSprite(string spriteName, int size, Func<int, int, Color> pixelFunction)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName + "Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = pixelFunction(x, y);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);

        sprite.name = spriteName;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
