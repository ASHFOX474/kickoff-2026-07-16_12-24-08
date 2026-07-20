#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KickoffCompleteGameSetup
{
    private const string SceneFolder = "Assets/Scenes";
    private const string ResourceFolder = "Assets/Resources";

    private struct LevelDefinition
    {
        public string FileName;
        public string DisplayName;
        public MazeBuilder.MazeLayoutType Layout;
        public MazeBuilder.VisualTheme Theme;
        public int Seed;
        public int Guards;
        public int Rooms;
        public int Connections;

        public LevelDefinition(
            string fileName,
            string displayName,
            MazeBuilder.MazeLayoutType layout,
            MazeBuilder.VisualTheme theme,
            int seed,
            int guards,
            int rooms,
            int connections)
        {
            FileName = fileName;
            DisplayName = displayName;
            Layout = layout;
            Theme = theme;
            Seed = seed;
            Guards = guards;
            Rooms = rooms;
            Connections = connections;
        }
    }

    private static readonly LevelDefinition[] Levels =
    {
        new LevelDefinition(
            "Level_01_Security_Facility.unity",
            "LEVEL 1 - STADIUM TUNNEL",
            MazeBuilder.MazeLayoutType.SecurityCompound,
            MazeBuilder.VisualTheme.StadiumVault,
            2101, 7, 7, 30),
        new LevelDefinition(
            "Level_02_Laboratory_Complex.unity",
            "LEVEL 2 - TRAINING GROUND",
            MazeBuilder.MazeLayoutType.ClassicLabyrinth,
            MazeBuilder.VisualTheme.TrainingGround,
            2202, 8, 9, 36),
        new LevelDefinition(
            "Level_03_Night_Industrial.unity",
            "LEVEL 3 - LOCKER ROOM",
            MazeBuilder.MazeLayoutType.TwinRoutes,
            MazeBuilder.VisualTheme.LockerRoom,
            2303, 9, 7, 34),
        new LevelDefinition(
            "Level_04_Mansion_Interior.unity",
            "LEVEL 4 - TROPHY ROOM",
            MazeBuilder.MazeLayoutType.SpiralLockdown,
            MazeBuilder.VisualTheme.TrophyRoom,
            2404, 10, 8, 30),
        new LevelDefinition(
            "Level_05_Overgrown_Ruins.unity",
            "LEVEL 5 - OLD STADIUM",
            MazeBuilder.MazeLayoutType.ArenaMaze,
            MazeBuilder.VisualTheme.OldStadium,
            2505, 11, 6, 40)
    };

    [InitializeOnLoadMethod]
    private static void ConfigureVisualsAfterReload()
    {
        EditorApplication.delayCall += ConfigureAllVisualAssets;
    }

    [MenuItem("Kickoff/1. Configure Cinematic Visual Assets")]
    public static void ConfigureAllVisualAssets()
    {
        if (!AssetDatabase.IsValidFolder(ResourceFolder))
        {
            return;
        }

        string[] imageGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ResourceFolder });
        int changedCount = 0;

        foreach (string guid in imageGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool isBackground = path.Contains("/Background") || path.Contains("/Backgrounds/");
            bool isPixelAsset = path.Contains("/Sprites/") || path.Contains("/Props/") || path.Contains("/Themes/");
            float pixelsPerUnit = isBackground ? 100f : 64f;
            FilterMode filterMode = isBackground ? FilterMode.Bilinear : FilterMode.Point;
            int maxSize = isBackground ? 2048 : 1024;

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.mipmapEnabled ||
                           importer.filterMode != filterMode ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed ||
                           importer.maxTextureSize != maxSize ||
                           Mathf.Abs(importer.spritePixelsPerUnit - pixelsPerUnit) > 0.01f;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = isPixelAsset && !isBackground;
            importer.mipmapEnabled = false;
            importer.filterMode = filterMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = maxSize;

            if (changed)
            {
                importer.SaveAndReimport();
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            Debug.Log($"Configured {changedCount} Kickoff cinematic sprites, tiles and backgrounds.");
        }
    }

    [MenuItem("Kickoff/2. Create Cinematic Five-Level Campaign")]
    public static void CreateCampaign()
    {
        ConfigureAllVisualAssets();
        EnsureSceneFolder();

        if (!EditorUtility.DisplayDialog(
                "Create cinematic campaign?",
                "This creates or replaces five playable level scenes and puts them in Build Settings in campaign order.",
                "Create Campaign",
                "Cancel"))
        {
            return;
        }

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        foreach (LevelDefinition level in Levels)
        {
            string scenePath = $"{SceneFolder}/{level.FileName}";
            CreateLevelScene(scenePath, level);
            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.OpenScene($"{SceneFolder}/{Levels[0].FileName}", OpenSceneMode.Single);
        Debug.Log("Created five cinematic stealth levels. Press Play in Level 1 to begin.");
    }

    [MenuItem("Kickoff/3. Rebuild Selected Maze Now")]
    public static void RebuildSelectedMaze()
    {
        MazeBuilder builder = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<MazeBuilder>()
            : null;

        if (builder == null)
        {
            builder = Object.FindFirstObjectByType<MazeBuilder>();
        }

        if (builder == null)
        {
            EditorUtility.DisplayDialog("No MazeBuilder", "Select the MazeBuilder object first.", "OK");
            return;
        }

        builder.BuildCompleteGame();
        EditorUtility.SetDirty(builder.gameObject);
        EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        Debug.Log($"Rebuilt {builder.layoutType} using {builder.visualTheme}.");
    }

    private static void CreateLevelScene(string scenePath, LevelDefinition level)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject managerObject = new GameObject("GameManager");
        GameManager manager = managerObject.AddComponent<GameManager>();
        manager.levelTitleOverride = level.DisplayName;

        GameObject mazeObject = new GameObject("MazeBuilder");
        MazeBuilder builder = mazeObject.AddComponent<MazeBuilder>();
        builder.width = 43;
        builder.height = 31;
        builder.cellSize = 1f;
        builder.randomSeed = level.Seed;
        builder.layoutType = level.Layout;
        builder.visualTheme = level.Theme;
        builder.floorColourVariation = 0.16f;
        builder.extraConnections = level.Connections;
        builder.roomCount = level.Rooms;
        builder.roomMinSize = 3;
        builder.roomMaxSize = 7;
        builder.guardCount = level.Guards;
        builder.hidingSpotCount = Mathf.Clamp(8 + level.Guards / 2, 9, 14);
        builder.patrolPointsPerGuard = 5;
        builder.playerVisualHeight = 0.98f;
        builder.guardVisualHeight = 0.80f;
        builder.guardIdentificationTime = Mathf.Lerp(1.85f, 1.35f, (level.Guards - 7) / 4f);
        builder.ambientPropCount = 30 + level.Guards * 2;
        builder.buildOnAwake = true;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.012f, 0.025f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<CameraFollow2D>();
        cameraObject.AddComponent<StealthScreenOverlay>();

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static void EnsureSceneFolder()
    {
        if (!AssetDatabase.IsValidFolder(SceneFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
#endif
