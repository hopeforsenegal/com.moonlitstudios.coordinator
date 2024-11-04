using System.IO;
using UnityEditor;
using UnityEngine;

public class ProjectSettings : ScriptableObject
{
    private const string CoordinatorSettingsResDir = "Assets/Coordinator/Resources";
    private const string CoordinatorSettingsFile = nameof(ProjectSettings);

    public static ProjectSettings LoadInstance()
    {
        var instance = Resources.Load<ProjectSettings>(CoordinatorSettingsFile);

        if (instance == null) {
            Directory.CreateDirectory(CoordinatorSettingsResDir);
            instance = CreateInstance<ProjectSettings>();
            instance.commandlineParams = new string[CoordinatorWindow.MaximumAmountOfEditors];
            instance.scriptingDefineSymbols = new string[CoordinatorWindow.MaximumAmountOfEditors];
#if UNITY_EDITOR
            AssetDatabase.CreateAsset(instance, Path.Combine(CoordinatorSettingsResDir, $"{CoordinatorSettingsFile}.asset"));
            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssetIfDirty(instance);
#endif
        }

        return instance;
    }

    public string[] commandlineParams; // pass -coordinator flag to everyone
    public string[] scriptingDefineSymbols;
    public string globalScriptingDefineSymbols;
}