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

    private readonly string[] options = { nameof(EditorType.Symlink), nameof(EditorType.HardCopy) };
    public struct EditorsVisible
    {
        public Vector2 ScrollPosition;
    }
    public struct Visible
    {
        public bool HasCoordinatePlay;
        public int IndexSelectedOption;
        public EditorsVisible Editors;
        public bool HasEditors;
        public float RefreshInterval;
        internal string[] EditorAvailable;
    }
    public struct Events
    {
        public int SelectEditorType;
        public bool EditorAdd;
        public string EditorOpen;
        public string EditorDelete;
        public string ShowInFinder;
        internal bool UpdateCoordinatePlay;
    }

    public Visible visible;

    void OnGUI()
    {
        var events = new Events();

        if (visible.RefreshInterval > 0) {
            visible.RefreshInterval -= Time.deltaTime;
        } else {
            visible.RefreshInterval = .5f; // Refresh every half second
            visible.EditorAvailable = Editors.GetEditorsAvailable();
        }

        /** Render **/
        if (visible.EditorAvailable.Length >= 2) {
            GUILayout.BeginVertical();
            {
                events.UpdateCoordinatePlay = GUILayout.Toggle(visible.HasCoordinatePlay, "Coordinate Play Mode");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Editor Creation Mode:");
                for (int i = 0; i < options.Length; i++) events.SelectEditorType = GUILayout.Toggle(visible.IndexSelectedOption == i, options[i]) ? i + 1 : 0;
                GUILayout.EndHorizontal();
                GUILayout.Space(10);

                GUILayout.Label("Available Editors:");

                visible.Editors.ScrollPosition = EditorGUILayout.BeginScrollView(visible.Editors.ScrollPosition);
                EditorGUILayout.LabelField("Global Preprocessor Defines");
                _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));

                foreach (var editor in visible.EditorAvailable) {
                    var editorInfo = EditorInfo.PopulateEditorInfo(editor);
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField(editorInfo.name);

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.TextField("Editor path", editorInfo.projectPath, EditorStyles.textField);
                    events.ShowInFinder = GUILayout.Button("Open in Finder") ? editorInfo.projectPath : events.ShowInFinder;
                    GUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Preprocessor Defines");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                    EditorGUILayout.LabelField("Command Line Params");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                    EditorGUILayout.LabelField("On Play Params");
                    _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));

                    events.EditorOpen = GUILayout.Button("Run Editor") ? editorInfo.projectPath : events.EditorOpen;
                    if (GUILayout.Button("Delete Editor")) {
                        events.EditorDelete = EditorUtility.DisplayDialog(
                            "Delete this editor?",
                            "Are you sure you want to delete this editor?",
                            "Delete",
                            "Cancel") ? editorInfo.projectPath : events.EditorDelete;
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

        /** Events **/
        if (events.SelectEditorType != default) {
            visible.IndexSelectedOption = events.SelectEditorType - 1;
            EditorUserSettings.Coordinator_EditorTypeOnCreate = (EditorType)visible.IndexSelectedOption;
        }
        if (events.UpdateCoordinatePlay) {
            visible.HasCoordinatePlay = !visible.HasCoordinatePlay;
            EditorUserSettings.Coordinator_EditorCoordinatePlay = visible.HasCoordinatePlay;
        }
        if (events.EditorAdd) {
            var path = Paths.ProjectPath;
            var original = EditorInfo.PopulateEditorInfo(path);
            var additional = EditorInfo.PopulateEditorInfo($"{path}Copy");

            Directory.CreateDirectory(additional.projectPath);
            if (EditorUserSettings.Coordinator_EditorTypeOnCreate == EditorType.Symlink) {
                Editors.Symlink(original.assetPath, additional.assetPath);
                Editors.Symlink(original.projectSettingsPath, additional.projectSettingsPath);
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