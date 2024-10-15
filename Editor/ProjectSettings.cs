using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoonlitSystem
{
    public class ProjectSettings : ScriptableObject
    {
        private const string CoordinatorSettingsResDir = "Assets/Coordinator/Resources";
        private const string CoordinatorSettingsFile = nameof(ProjectSettings);
        private const string CoordinatorSettingsFileExtension = ".asset";

        public static ProjectSettings LoadInstance()
        {
            var instance = Resources.Load<ProjectSettings>(CoordinatorSettingsFile);

            if (instance == null) {
                Directory.CreateDirectory(CoordinatorSettingsResDir);
                instance = CreateInstance<ProjectSettings>();
                instance.commandlineParams = "-coordinator";
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(instance, Path.Combine(CoordinatorSettingsResDir, $"{CoordinatorSettingsFile}{CoordinatorSettingsFileExtension}"));
                AssetDatabase.SaveAssets();
#endif
            }

            return instance;
        }

        public string commandlineParams; // pass -coordinator flag to everyone

        // Preprocessor Defines, Command line Params, & On Play Param, should not differ from person to person (just whether or not they are used for that run/session is)
        // So it makes a lot of sense that they will be stored and perhaps editable here!
    }
}