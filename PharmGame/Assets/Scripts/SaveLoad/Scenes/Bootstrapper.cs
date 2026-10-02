using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Ensures core persistent services are initialized and redirects 
/// Play Mode in the Editor to the primary Bootstrapper scene.
/// </summary>
public class Bootstrapper : PersistentSingleton<Bootstrapper>
{
    private const int BootstrapperSceneIndex = 0;

#if UNITY_EDITOR
    /// <summary>
    /// Executes when the Editor launches or recompiles scripts.
    /// Sets the Play Mode start scene prior to entering Play Mode.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void ConfigureEditorPlayModeStartScene()
    {
        // Guard against projects with empty Build Settings
        if (EditorBuildSettings.scenes.Length <= BootstrapperSceneIndex)
        {
            Debug.LogWarning("[Bootstrapper] Build Settings contain no valid scene at index 0.");
            return;
        }

        string scenePath = EditorBuildSettings.scenes[BootstrapperSceneIndex].path;
        SceneAsset bootstrapperSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

        if (bootstrapperSceneAsset != null)
        {
            EditorSceneManager.playModeStartScene = bootstrapperSceneAsset;
        }
        else
        {
            Debug.LogWarning($"[Bootstrapper] Failed to load SceneAsset at build path: {scenePath}");
        }
    }
#endif
}