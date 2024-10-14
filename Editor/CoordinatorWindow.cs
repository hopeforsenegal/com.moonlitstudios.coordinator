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
        public EditorsVisible Editors;
        public bool HasEditors;
    }
    public struct Events
    {
        public bool AddEditor;
        internal bool ShowEditors;
    }

    public Visible visible;

    void OnGUI()
    {
        var events = new Events();
        visible.HasEditors = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.T ? !visible.HasEditors : visible.HasEditors;

        /** Render **/
        var availableEditors = Editors.GetEditorsAvailable();
        if (availableEditors.Length >= 2) {
            GUILayout.BeginVertical();
            {
                GUILayout.Label("Available Editors:");

                visible.Editors.ScrollPosition = EditorGUILayout.BeginScrollView(visible.Editors.ScrollPosition);

                foreach (var editor in availableEditors) {
                    var editorInfo = Editors.Editor.PopulateEditorInfo(editor);
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField(editorInfo.name);
                    GUILayout.EndVertical();
                }

                EditorGUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        } else {
            EditorGUILayout.HelpBox("Nothing to coordinate with. No additional editors are available yet.", MessageType.Info);
            events.AddEditor = GUILayout.Button("Add a symlink editor");
        }

        events.ShowEditors = GUILayout.Button("Show editors in Finder");

        /** Events **/
        if (events.AddEditor) {
            var path = Editors.ProjectPath;
            var currentEditor = Editors.Editor.PopulateEditorInfo(path);
            var newEditor = Editors.Editor.PopulateEditorInfo($"{path}Copy");
            Editors.CreateSymlinkEditor(currentEditor, newEditor);
        }
        if (events.ShowEditors) {
            System.Diagnostics.Process.Start(Editors.ProjectPath);
        }
    }
}