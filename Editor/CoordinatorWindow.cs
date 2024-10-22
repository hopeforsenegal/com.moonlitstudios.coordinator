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

    public const int MaximumAmountOfEditors = 6;
    public struct Visible
    {
        public Vector2 ScrollPosition;
        public bool HasCoordinatePlay;
        public bool PreviousHasCoordinatePlay;
        public string[] ScriptingDefineSymbols;
        public string[] PreviousScriptingDefineSymbols;
        public float RefreshInterval;
        public string[] EditorAvailable;
        public PathToProcessId[] PathToProcessIds;
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

    private static Visible sVisible;
    private static ProjectSettings sProjectSettingsInMemory;

    protected void CreateGUI()
    {
        sVisible.HasCoordinatePlay = EditorUserSettings.Coordinator_EditorCoordinatePlay;
        sVisible.ScriptingDefineSymbols = new string[MaximumAmountOfEditors];
        sVisible.PreviousScriptingDefineSymbols = new string[MaximumAmountOfEditors];
        sProjectSettingsInMemory = ProjectSettings.LoadInstance();

        var existingDefines = sProjectSettingsInMemory.scriptingDefineSymbols;
        for (int i = 0; i < MaximumAmountOfEditors && i < existingDefines.Length; i++) {
            UnityEngine.Debug.Log($"Retrieving {existingDefines[i]} at {i}");
            sVisible.ScriptingDefineSymbols[i] = existingDefines[i];
            sVisible.PreviousScriptingDefineSymbols[i] = existingDefines[i];
        }
        EditorApplication.playModeStateChanged += OriginalCoordinatePlaymodeStateChanged; // Duplicated from Editors for convenience (its more code to make this a singleton simply to bypass this)
    }

    protected void OnGUI()
    {
        var events = new Events();

        if (sVisible.RefreshInterval > 0) {
            sVisible.RefreshInterval -= Time.deltaTime;
        } else {
            sVisible.RefreshInterval = .5f; // Refresh every half second
            ////////////////////////
            var pathToProcessIds = PathToProcessId.Split(UntilExitSettings.Coordinator_ProjectPathToChildProcessID);
            var updatedListOfProcesses = new List<PathToProcessId>();
            foreach (var p in pathToProcessIds) {
                if (Editors.IsProcessAlive(p.processID)) {
                    updatedListOfProcesses.Add(p);
                }
            }

            sVisible.PathToProcessIds = updatedListOfProcesses.ToArray();
            sVisible.EditorAvailable = Editors.GetEditorsAvailable();
        }

        /*- UI -*/
        GUILayout.BeginHorizontal();
        events.Settings = GUILayout.Button("Settings");
        events.Github = GUILayout.Button("Github");
        GUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        if (Editors.IsAdditional()) {
            EditorGUILayout.HelpBox($"You can only launch additional editors from the original editor.", MessageType.Info);
        } else {
            if (sVisible.EditorAvailable.Length >= 2) {
                GUILayout.BeginVertical();
                {
                    GUILayout.Space(10);
                    sVisible.HasCoordinatePlay = GUILayout.Toggle(sVisible.HasCoordinatePlay, "Coordinate Play Mode");
                    events.UpdateCoordinatePlay = sVisible.HasCoordinatePlay != sVisible.PreviousHasCoordinatePlay;
                    GUILayout.Space(10);

                    GUILayout.Label("Available Editors:");
                    sVisible.ScrollPosition = EditorGUILayout.BeginScrollView(sVisible.ScrollPosition);

                    for (int i = 0; i < sVisible.EditorAvailable.Length; i++) {
                        string editor = sVisible.EditorAvailable[i];
                        var editorInfo = EditorPaths.PopulateEditorInfo(editor);
                        var isRunningProject = false;
                        foreach (var p in sVisible.PathToProcessIds) {
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

                        EditorGUI.BeginDisabledGroup(isRunningProject);
                        EditorGUILayout.LabelField("Command Line Params");
                        _ = EditorGUILayout.TextArea("temp", GUILayout.Height(40), GUILayout.MaxWidth(200));
                        EditorGUI.EndDisabledGroup();

                        if (sVisible.HasCoordinatePlay) {
                            EditorGUILayout.LabelField("OVERWRITE Scripting Define Symbols on Play (We will improve this in the future)");
                            GUILayout.BeginHorizontal();
                            sVisible.ScriptingDefineSymbols[i] = EditorGUILayout.TextArea(sVisible.ScriptingDefineSymbols[i], GUILayout.Height(40), GUILayout.MaxWidth(200));
                            GUILayout.EndHorizontal();
                        }

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

            events.EditorAdd = (sVisible.EditorAvailable.Length < MaximumAmountOfEditors) && GUILayout.Button($"Add a {EditorUserSettings.Coordinator_EditorTypeOnCreate} Editor");
            EditorGUI.EndDisabledGroup();
            events.ShowInFinder = GUILayout.Button("Show Editors in Finder") ? Paths.ProjectRootPath : events.ShowInFinder;
        }

        /*- Handle Events -*/
        if (events.Github) {
            Application.OpenURL("https://github.com/hopeforsenegal/com.moonlitstudios.coordinator");
        }
        if (events.Settings) {
            SettingsService.OpenProjectSettings(CoordinatorSettingsProvider.MenuLocationInProjectSettings);
        }
        if (events.UpdateCoordinatePlay) {
            EditorUserSettings.Coordinator_EditorCoordinatePlay = sVisible.HasCoordinatePlay;
        }
        if (events.EditorAdd) {
            var path = Paths.ProjectPath;
            var original = EditorPaths.PopulateEditorInfo(path);
            var additional = EditorPaths.PopulateEditorInfo($"{path}Copy");

            Directory.CreateDirectory(additional.Path);
            if (EditorUserSettings.Coordinator_EditorTypeOnCreate == EditorType.Symlink) {
                Editors.Symlink(original.Assets, additional.Assets);
                Editors.Symlink(original.ProjectSettings, additional.ProjectSettings);
                Editors.Symlink(original.Packages, additional.Packages); // There is a world where you want different packages on your  In that case this part should be controlled by a setting. Defaulting to symlink for now though
                // -- TODO mark that this is a symlink project and not a copy... so that the UI can show it!
            } else {
                UnityEngine.Debug.Assert(false, "TODO !");
            }
        }
        if (!string.IsNullOrWhiteSpace(events.EditorOpen)) {
            UnityEngine.Debug.Assert(Directory.Exists(events.EditorOpen), "No Editor at location");
            var process = Process.Start($"{EditorApplication.applicationPath}/Contents/MacOS/Unity", $"-projectPath \"{events.EditorOpen}\" {CommandLineParams.AdditionalEditorParams}");
            var processIds = new List<PathToProcessId>(sVisible.PathToProcessIds);
            processIds.Add(new PathToProcessId { path = events.EditorOpen, processID = process.Id });
            UntilExitSettings.Coordinator_ProjectPathToChildProcessID = PathToProcessId.Join(processIds.ToArray());
            sVisible.PathToProcessIds = processIds.ToArray();
        }
        if (!string.IsNullOrWhiteSpace(events.EditorClose)) {
            var pathToProcessIds = sVisible.PathToProcessIds;
            foreach (var p in pathToProcessIds) {
                if (p.path == events.EditorClose) {
                    Process.GetProcessById(p.processID).Kill(); // Is calling Kill() twice bad? Probably not so we don't need to update local memory
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

    private void OriginalCoordinatePlaymodeStateChanged(PlayModeStateChange playmodeState)
    {
        if (playmodeState == PlayModeStateChange.ExitingEditMode) {
            UnityEngine.Debug.Log("Saving scripting defines");
            sProjectSettingsInMemory.scriptingDefineSymbols = sVisible.ScriptingDefineSymbols;
            AssetDatabase.SaveAssetIfDirty(sProjectSettingsInMemory);
        }
    }
}