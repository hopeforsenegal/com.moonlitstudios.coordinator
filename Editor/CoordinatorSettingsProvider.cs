using MoonlitSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    public class CoordinatorSettingsProvider : SettingsProvider
    {
        private readonly string[] EditorCreationOptions = { nameof(EditorType.Symlink), nameof(EditorType.HardCopy) };
        public const string MenuLocationInProjectSettings = "Project/Coordinator";

        public struct Events
        {
            public int SelectEditorType;
        }
        public struct Visible
        {
            public int IndexSelectedOption;
        }

        private SerializedObject m_ProjectSettings;
        private SerializedProperty m_CommandLineParams;
        private Visible m_Visible;

        private CoordinatorSettingsProvider(string path, SettingsScope scope = SettingsScope.Project) : base(path, scope) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_ProjectSettings = new SerializedObject(ProjectSettings.LoadInstance());
            m_CommandLineParams = m_ProjectSettings.FindProperty(nameof(ProjectSettings.commandlineParams));
        }

        public override void OnGUI(string searchContext)
        {
            var events = new Events();

            /*- Render -*/
            GUILayout.Label("User/Editor Coordinator Settings:", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Editor Creation Mode:");
            for (var i = 0; i < EditorCreationOptions.Length; i++) events.SelectEditorType = GUILayout.Toggle(m_Visible.IndexSelectedOption == i, EditorCreationOptions[i]) ? i + 1 : 0;
            // Things to consider are things like do we copy the library folder
            // And what additional folders should we copy
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.Label("Project Coordinator Settings:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_CommandLineParams);

            /*- Events -*/
            if (events.SelectEditorType != default) {
                m_Visible.IndexSelectedOption = events.SelectEditorType - 1;
                EditorUserSettings.Coordinator_EditorTypeOnCreate = (EditorType)m_Visible.IndexSelectedOption;
            }

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