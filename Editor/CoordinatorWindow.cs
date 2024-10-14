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
    }

    public Visible visible;

    void OnGUI()
    {
        var events = new Events();
        visible.HasEditors = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.T ? !visible.HasEditors : visible.HasEditors;

        if (visible.HasEditors) {
            GUILayout.BeginVertical();
            {
                GUILayout.Label("Available Editors:");

                visible.Editors.ScrollPosition = EditorGUILayout.BeginScrollView(visible.Editors.ScrollPosition);
                EditorGUILayout.EndScrollView();

            }
            GUILayout.EndVertical();
        } else {
            EditorGUILayout.HelpBox("Nothing to coordinate with. No additional editors are available yet.", MessageType.Info);
            events.AddEditor = GUILayout.Button("Add a symlink editor");
        }

        if (events.AddEditor) {
        }
    }
}