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
        public string[] EditorAvailable;
    }
    public struct Events
    {
        public bool EditorAdd;
        public string EditorOpen;
        public string EditorDelete;
        public string ShowInFinder;
        public bool UpdateCoordinatePlay;
        internal bool Settings;
        internal bool Github;
    }

    private Visible m_Visible;
    public const int MaximumAmountOfEditors = 6;

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
                GUILayout.BeginHorizontal();
                events.Settings = GUILayout.Button("Settings");
                events.Github = GUILayout.Button("Github");
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                events.UpdateCoordinatePlay = GUILayout.Toggle(m_Visible.HasCoordinatePlay, "Coordinate Play Mode");
                GUILayout.Space(10);

                GUILayout.Label("Available Editors:");
                m_Visible.Editors.ScrollPosition = EditorGUILayout.BeginScrollView(m_Visible.Editors.ScrollPosition);
                EditorGUILayout.LabelField("Global Preprocessor Defines");
                _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));

                foreach (var editor in m_Visible.EditorAvailable) {
                    var editorInfo = EditorPaths.PopulateEditorInfo(editor);
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField(editorInfo.Name);

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.TextField("Editor path", editorInfo.Path, EditorStyles.textField);
                    events.ShowInFinder = GUILayout.Button("Open in Finder") ? editorInfo.Path : events.ShowInFinder;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Preprocessor Defines");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                    EditorGUILayout.LabelField("Command Line Params");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                    EditorGUILayout.LabelField("On Play Params");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));

                    events.EditorOpen = GUILayout.Button("Run Editor") ? editorInfo.Path : events.EditorOpen;
                    if (GUILayout.Button("Delete Editor")) {
                        events.EditorDelete = EditorUtility.DisplayDialog(
                            "Delete this editor?",
                            "Are you sure you want to delete this editor?",
                            "Delete",
                            "Cancel") ? editorInfo.Path : events.EditorDelete;
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

        events.EditorAdd = (m_Visible.EditorAvailable.Length < MaximumAmountOfEditors) && GUILayout.Button($"Add a {EditorUserSettings.Coordinator_EditorTypeOnCreate} Editor");
        events.ShowInFinder = GUILayout.Button("Show editors in Finder") ? Paths.ProjectPath : events.ShowInFinder;

        /*- Events -*/
        if (events.Github) {
            Application.OpenURL("https://github.com/hopeforsenegal/com.moonlitstudios.coordinator");
        }
        if (events.Settings) {
            SettingsService.OpenProjectSettings(Editor.CoordinatorSettingsProvider.MenuLocationInProjectSettings);
        }
        if (events.UpdateCoordinatePlay) {
            m_Visible.HasCoordinatePlay = !m_Visible.HasCoordinatePlay;
            EditorUserSettings.Coordinator_EditorCoordinatePlay = m_Visible.HasCoordinatePlay;
        }
        if (events.EditorAdd) {
            var path = Paths.ProjectPath;
            var original = EditorPaths.PopulateEditorInfo(path);
            var additional = EditorPaths.PopulateEditorInfo($"{path}Copy");

            Directory.CreateDirectory(additional.Path);
            if (EditorUserSettings.Coordinator_EditorTypeOnCreate == EditorType.Symlink) {
                Editors.Symlink(original.Assets, additional.Assets);
                Editors.Symlink(original.ProjectSettings, additional.ProjectSettings);
                Editors.Symlink(original.Packages, additional.Packages); // There is a world where you want different packages on your editors. In that case this part should be controlled by a setting. Defaulting to symlink for now though
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
            UnityEngine.Debug.Assert(Directory.Exists(events.ShowInFinder), "Not a valid location");
            Process.Start(events.ShowInFinder);
        }
    }
}