using MoonlitSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    public class CoordinatorSettingsProvider : SettingsProvider
    {
        public const string MenuLocationInProjectSettings = "Project/Coordinator";

        private SerializedObject m_ProjectSettings;
        private SerializedProperty m_Unused;

        private CoordinatorSettingsProvider(string path, SettingsScope scope = SettingsScope.Project) : base(path, scope) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_ProjectSettings = new SerializedObject(CoordinatorProjectSettings.LoadInstance());
            m_Unused = m_ProjectSettings.FindProperty(nameof(CoordinatorProjectSettings.unused));
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_Unused, new GUIContent("-Placeholder-"));
            EditorGUI.EndDisabledGroup();

            m_ProjectSettings.ApplyModifiedProperties();
        }

        [MenuItem("Moonlit/Coordinator/Settings", priority = 0)]
        private static void SendToProjectSettings()
        {
            SettingsService.OpenProjectSettings(MenuLocationInProjectSettings);
        }

        [SettingsProvider]
        public static SettingsProvider CreateCoordinatorSettingsProvider()
        {
            return new CoordinatorSettingsProvider(MenuLocationInProjectSettings);
        }
    }
}