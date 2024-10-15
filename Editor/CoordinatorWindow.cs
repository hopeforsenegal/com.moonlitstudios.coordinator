using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CoordinatorWindow : EditorWindow
{
    [MenuItem("Moonlit/Coordinator/Coordinate")]
    public static void ShowWindow()
    {
        GetWindow(typeof(CoordinatorWindow));
    }

    public struct EditorsVisible
    {
        public Vector2 ScrollPosition;
    }
    public struct Visible
    {
        public bool HasCoordinatePlay;
        public EditorsVisible Editors;
        public float RefreshInterval;
        internal string[] EditorAvailable;
    }
    public struct Events
    {
        public bool EditorAdd;
        public string EditorOpen;
        public string EditorDelete;
        public string ShowInFinder;
        internal bool UpdateCoordinatePlay;
    }

    private Visible m_Visible;

    protected void OnGUI()
    {
        var events = new Events();

        if (m_Visible.RefreshInterval > 0) {
            m_Visible.RefreshInterval -= Time.deltaTime;
        } else {
            m_Visible.RefreshInterval = .5f; // Refresh every half second
            m_Visible.EditorAvailable = Editors.GetEditorsAvailable();
        }

        /*- Render -*/
        if (m_Visible.EditorAvailable.Length >= 2) {
            GUILayout.BeginVertical();
            {
                events.UpdateCoordinatePlay = GUILayout.Toggle(m_Visible.HasCoordinatePlay, "Coordinate Play Mode");
                GUILayout.Space(10);

                GUILayout.Label("Available Editors:");
                m_Visible.Editors.ScrollPosition = EditorGUILayout.BeginScrollView(m_Visible.Editors.ScrollPosition);
                EditorGUILayout.LabelField("Global Preprocessor Defines");
                _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));

                foreach (var editor in m_Visible.EditorAvailable) {
                    var editorInfo = EditorInfo.PopulateEditorInfo(editor);
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField(editorInfo.Name);

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.TextField("Editor path", editorInfo.ProjectPath, EditorStyles.textField);
                    events.ShowInFinder = GUILayout.Button("Open in Finder") ? editorInfo.ProjectPath : events.ShowInFinder;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Preprocessor Defines");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                    EditorGUILayout.LabelField("Command Line Params");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                    EditorGUILayout.LabelField("On Play Params");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));

                    events.EditorOpen = GUILayout.Button("Run Editor") ? editorInfo.ProjectPath : events.EditorOpen;
                    if (GUILayout.Button("Delete Editor")) {
                        events.EditorDelete = EditorUtility.DisplayDialog(
                            "Delete this editor?",
                            "Are you sure you want to delete this editor?",
                            "Delete",
                            "Cancel") ? editorInfo.ProjectPath : events.EditorDelete;
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(50);
                }

                EditorGUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        } else {
            EditorGUILayout.HelpBox("Nothing to coordinate with. No additional editors are available yet.", MessageType.Info);
        }

        events.EditorAdd = GUILayout.Button($"Add a {EditorUserSettings.Coordinator_EditorTypeOnCreate} Editor");
        events.ShowInFinder = GUILayout.Button("Show editors in Finder") ? Paths.ProjectPath : events.ShowInFinder;

        /*- Events -*/
        if (events.UpdateCoordinatePlay) {
            m_Visible.HasCoordinatePlay = !m_Visible.HasCoordinatePlay;
            EditorUserSettings.Coordinator_EditorCoordinatePlay = m_Visible.HasCoordinatePlay;
        }
        if (events.EditorAdd) {
            var path = Paths.ProjectPath;
            var original = EditorInfo.PopulateEditorInfo(path);
            var additional = EditorInfo.PopulateEditorInfo($"{path}Copy");

            Directory.CreateDirectory(additional.ProjectPath);
            if (EditorUserSettings.Coordinator_EditorTypeOnCreate == EditorType.Symlink) {
                Editors.Symlink(original.AssetPath, additional.AssetPath);
                Editors.Symlink(original.ProjectSettingsPath, additional.ProjectSettingsPath);
                // -- TODO mark that this is a symlink project and not a copy... so that the UI can show it!
            } else {
                UnityEngine.Debug.Assert(false, "TODO !");
            }
        }
        if (!string.IsNullOrWhiteSpace(events.EditorOpen)) {
            UnityEngine.Debug.Assert(Directory.Exists(events.EditorOpen), "No Editor at location");

            Process.Start($"{EditorApplication.applicationPath}/Contents/MacOS/Unity", $"-projectPath \"{events.EditorOpen}\" {CommandLineParams.AdditionalEditorParams}");
        }
        if (!string.IsNullOrWhiteSpace(events.EditorDelete)) {
            FileUtil.DeleteFileOrDirectory(events.EditorDelete);
        }
        if (!string.IsNullOrWhiteSpace(events.ShowInFinder)) {
            Process.Start(events.ShowInFinder);
        }
    }
}