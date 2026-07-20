#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Kickoff/0. Create Intro Cutscene Scene
/// Builds "Assets/Scenes/Intro.unity" containing the IntroCutscene player,
/// and makes sure it is build-index 0 so it plays before Level 1.
/// Safe to re-run - it just overwrites the Intro scene, it won't touch your levels.
/// </summary>
public static class KickoffIntroSetup
{
    private const string ScenePath = "Assets/Scenes/Intro.unity";

    [MenuItem("Kickoff/0. Create Intro Cutscene Scene")]
    public static void CreateIntroScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject introObject = new GameObject("IntroCutscene");
        IntroCutscene intro = introObject.AddComponent<IntroCutscene>();

        // Point it at whatever your first playable level is called.
        // Change this if your Level 1 scene has a different name.
        intro.nextSceneName = "Level_01_Security_Facility";

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

        // Put Intro first in Build Settings, keep every other scene already listed.
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Intro scene created and set as build index 0. Set IntroCutscene.nextSceneName if your first level isn't Level_01_Security_Facility.");
    }
}
#endif
