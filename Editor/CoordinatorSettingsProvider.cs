using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class CoordinatorSettingsProvider : SettingsProvider
{
    public const string MenuLocationInProjectSettings = "Project/Coordinator";

    private SerializedObject m_ProjectSettings;
    private SerializedProperty m_CommandLineParams;
    private SerializedProperty m_ScriptingDefineSymbols;
    private SerializedProperty m_GlobalScriptingDefineSymbols;

    private CoordinatorSettingsProvider(string path, SettingsScope scope = SettingsScope.Project) : base(path, scope) { }

    [SettingsProvider]
    public static SettingsProvider CreateCoordinatorSettingsProvider() => new CoordinatorSettingsProvider(MenuLocationInProjectSettings);

    public override void OnActivate(string searchContext, VisualElement rootElement)
    {
        m_ProjectSettings = new SerializedObject(ProjectSettings.LoadInstance());
        m_CommandLineParams = m_ProjectSettings.FindProperty(nameof(ProjectSettings.commandlineParams));
        m_ScriptingDefineSymbols = m_ProjectSettings.FindProperty(nameof(ProjectSettings.scriptingDefineSymbols));
        m_GlobalScriptingDefineSymbols = m_ProjectSettings.FindProperty(nameof(ProjectSettings.globalScriptingDefineSymbols));
    }

    public override void OnGUI(string searchContext)
    {
        /*- Render -*/
        GUILayout.Label("User/Editor Coordinator Settings:", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("Project Coordinator Settings:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_CommandLineParams);
        EditorGUILayout.PropertyField(m_ScriptingDefineSymbols);
        EditorGUILayout.PropertyField(m_GlobalScriptingDefineSymbols);

        /*- Events -*/
        m_ProjectSettings.ApplyModifiedProperties();
    }
}