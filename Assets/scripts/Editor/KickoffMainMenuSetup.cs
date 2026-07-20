#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Kickoff/-1. Create Main Menu Scene
/// Builds "Assets/Scenes/MainMenu.unity" and makes it build-index 0.
/// Safe to re-run - only overwrites the menu scene itself.
/// </summary>
public static class KickoffMainMenuSetup
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Kickoff/-1. Create Main Menu Scene")]
    public static void CreateMainMenu()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject menuObject = new GameObject("MainMenu");
        KickoffMainMenu menu = menuObject.AddComponent<KickoffMainMenu>();
        menu.playSceneName = "Intro"; // change if your intro scene has a different name

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.35f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        cameraObject.AddComponent<AudioListener>();

        EditorSceneManager.SaveScene(scene, ScenePath);

        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Main menu created and set as build index 0.");
    }
}
#endif
