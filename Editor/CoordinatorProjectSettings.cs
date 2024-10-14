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
                var assetPath = Path.Combine(CoordinatorSettingsResDir, CoordinatorSettingsFile + CoordinatorSettingsFileExtension);
                instance = CreateInstance<CoordinatorProjectSettings>();
                instance.unused = "Here temporarily until real settings get added.";
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
#endif
            }

            return instance;
        }

        public string unused; // @placeholder. Here temporarily until real settings get added.
    }
}