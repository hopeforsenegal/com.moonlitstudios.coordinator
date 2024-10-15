using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoonlitSystem
{
    public class CoordinatorProjectSettings : ScriptableObject
    {
        private const string CoordinatorSettingsResDir = "Assets/Coordinator/Resources";
        private const string CoordinatorSettingsFile = "CoordinatorSettings";
        private const string CoordinatorSettingsFileExtension = ".asset";

        public static CoordinatorProjectSettings LoadInstance()
        {
            var instance = Resources.Load<CoordinatorProjectSettings>(CoordinatorSettingsFile);

            if (instance == null) {
                Directory.CreateDirectory(CoordinatorSettingsResDir);
                instance = CreateInstance<CoordinatorProjectSettings>();
                instance.unused = "Here temporarily until real settings get added.";
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(instance, Path.Combine(CoordinatorSettingsResDir, $"{CoordinatorSettingsFile}{CoordinatorSettingsFileExtension}"));
                AssetDatabase.SaveAssets();
#endif
            }

            return instance;
        }

        public string unused; // @placeholder. Here temporarily until real settings get added.

        // Preprocessor Defines, Command line Params, & On Play Param, should not differ from person to person (just whether or not they are used for that run/session is)
        // So it makes alot of sense that they will be stored and perhaps editable here!
    }
}