using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CoordinatorWindow : EditorWindow
{
    [MenuItem("Moonlit/Coordinator/Settings", priority = 0)]
    private static void SendToProjectSettings() => SettingsService.OpenProjectSettings(CoordinatorSettingsProvider.MenuLocationInProjectSettings);

    [MenuItem("Moonlit/Coordinator/Github", priority = 0)]
    private static void SendToGithub() => Application.OpenURL("https://github.com/hopeforsenegal/com.moonlitstudios.coordinator");

    [MenuItem("Moonlit/Coordinator/Coordinate", priority = 20)]
    public static void ShowWindow() => GetWindow(typeof(CoordinatorWindow));

    public struct EditorsVisible { public Vector2 ScrollPosition; }
    public struct Visible
    {
        public bool HasCoordinatePlay;
        public EditorsVisible Editors;
        public float RefreshInterval;
        public string[] EditorAvailable;
        internal PathToProcessId[] PathToProcessIds;
    }
    public struct Events
    {
        public bool EditorAdd;
        public string EditorOpen;
        public string EditorClose;
        public string EditorDelete;
        public string ShowInFinder;
        public bool UpdateCoordinatePlay;
        public bool Settings;
        public bool Github;
    }

    private Visible m_Visible;
    public const int MaximumAmountOfEditors = 6;
    readonly public string[] scriptingDefineSymbols = new string[MaximumAmountOfEditors];

    protected void OnGUI()
    {
        var events = new Events();

        if (m_Visible.RefreshInterval > 0) {
            m_Visible.RefreshInterval -= Time.deltaTime;
        } else {
            m_Visible.RefreshInterval = .5f; // Refresh every half second
            ////////////////////////
            var pathToProcessIds = PathToProcessId.Split(UntilExitSettings.Coordinator_ProjectPathToChildProcessID);
            var updatedListOfProcesses = new List<PathToProcessId>();
            foreach (var p in pathToProcessIds) {
                if (Editors.IsProcessAlive(p.processID)) {
                    updatedListOfProcesses.Add(p);
                }
            }

            m_Visible.PathToProcessIds = updatedListOfProcesses.ToArray();
            m_Visible.EditorAvailable = Editors.GetEditorsAvailable();
        }

        /*- Render -*/
        GUILayout.BeginHorizontal();
        events.Settings = GUILayout.Button("Settings");
        events.Github = GUILayout.Button("Github");
        GUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        if (Editors.IsAdditional()) {
            EditorGUILayout.HelpBox($"You can only launch additional editors from the original editor.", MessageType.Info);
        } else {
            if (m_Visible.EditorAvailable.Length >= 2) {
                GUILayout.BeginVertical();
                {
                    GUILayout.Space(10);
                    events.UpdateCoordinatePlay = GUILayout.Toggle(m_Visible.HasCoordinatePlay, "Coordinate Play Mode");
                    GUILayout.Space(10);

                    GUILayout.Label("Available Editors:");
                    m_Visible.Editors.ScrollPosition = EditorGUILayout.BeginScrollView(m_Visible.Editors.ScrollPosition);

                    for (int i = 0; i < m_Visible.EditorAvailable.Length; i++) {
                        string editor = m_Visible.EditorAvailable[i];
                        var editorInfo = EditorPaths.PopulateEditorInfo(editor);
                        var isRunningProject = false;
                        foreach (var p in m_Visible.PathToProcessIds) {
                            if (p.path == editorInfo.Path) {
                                isRunningProject = true;
                                break;
                            }
                        }
                        GUILayout.BeginVertical();
                        EditorGUILayout.LabelField(editorInfo.Name);

                        GUILayout.BeginHorizontal();
                        EditorGUILayout.TextField("Editor path", editorInfo.Path, EditorStyles.textField);
                        events.ShowInFinder = GUILayout.Button("Open in Finder") ? editorInfo.Path : events.ShowInFinder;
                        GUILayout.EndHorizontal();


                        // store the [scripting define symbols] in the project settings
                        //      How do we do this per Editor in a non clunky way?
                        // the additional editors are going to pull it from the project settings (which is symlinked by default)
                        // pull current symbols and join it with the ones from project settings
                        // then go into play (since this is the only time it matters)
                        // then when exiting play remove them (since we don't want to permanently alter the project)


                        EditorGUI.BeginDisabledGroup(isRunningProject);
                        EditorGUILayout.LabelField("Command Line Params");
                        _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.LabelField("Scripting Define Symbols (Updates on editor before 'Play')");
                        scriptingDefineSymbols[i] = EditorGUILayout.TextArea(scriptingDefineSymbols[i], GUILayout.Height(40), GUILayout.MaxWidth(200));
                        EditorGUILayout.LabelField("On Play Params");
                        _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));

                        GUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(isRunningProject);
                        events.EditorOpen = GUILayout.Button("Open Editor") ? editorInfo.Path : events.EditorOpen;
                        EditorGUI.EndDisabledGroup();
                        EditorGUI.BeginDisabledGroup(!isRunningProject);
                        events.EditorClose = GUILayout.Button("Close Editor") ? editorInfo.Path : events.EditorClose;
                        EditorGUI.EndDisabledGroup();
                        GUILayout.EndHorizontal();
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
            EditorGUI.EndDisabledGroup();
            events.ShowInFinder = GUILayout.Button("Show Editors in Finder") ? Paths.ProjectRootPath : events.ShowInFinder;
        }

        /*- Events -*/
        if (events.Github) {
            Application.OpenURL("https://github.com/hopeforsenegal/com.moonlitstudios.coordinator");
        }
        if (events.Settings) {
            SettingsService.OpenProjectSettings(CoordinatorSettingsProvider.MenuLocationInProjectSettings);
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
            var process = Process.Start($"{EditorApplication.applicationPath}/Contents/MacOS/Unity", $"-projectPath \"{events.EditorOpen}\" {CommandLineParams.AdditionalEditorParams}");
            var processIds = new List<PathToProcessId>(m_Visible.PathToProcessIds);
            processIds.Add(new PathToProcessId { path = events.EditorOpen, processID = process.Id });
            UntilExitSettings.Coordinator_ProjectPathToChildProcessID = PathToProcessId.Join(processIds.ToArray());
            m_Visible.PathToProcessIds = processIds.ToArray();
        }
        if (!string.IsNullOrWhiteSpace(events.EditorClose)) {
            var pathToProcessIds = m_Visible.PathToProcessIds;
            foreach (var p in pathToProcessIds) {
                if (p.path == events.EditorClose) {
                    Process.GetProcessById(p.processID).Kill(); // Is calling Kill twice bad? probably not so we don't need to update local memory
                    break;
                }
            }
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