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

    private struct Visible
    {
        public Vector2 ScrollPosition;
        public float RefreshInterval;
        public CoordinationModes CoordinationMode;
        // NOTE: We are Struct of arrays (instead of Array of structs). This is to ensure our compatibility of the string arrays across domains (ex. ProjectSettings)
        public string[] ScriptingDefineSymbols;
        public string[] PreviousScriptingDefineSymbols;
        public string[] CommandLineParams;
        public string[] Path;
        public bool[] IsShowFoldout;
        public bool[] IsSymlinked;
        public PathToProcessId[] PathToProcessIds;
    }

    private struct Events
    {
        public int Index;
        public EditorType EditorAdd;
        public string EditorOpen;
        public string EditorClose;
        public string EditorDelete;
        public string ShowInFinder;
        public bool UpdateCoordinatePlay;
        public bool Settings;
        public bool Github;
        public bool StartTests;
        public bool StopTests;
    }

    private static Visible sVisible;
    private static ProjectSettings sProjectSettingsInMemory;
    private static int sSelectedOption, sPreviousSelection = 0;
    private static readonly Color Red = new Color(255 / 255f, 235 / 255f, 235 / 255f);
    private static readonly string[] Options = { CoordinationModes.Standalone.ToString(), CoordinationModes.Playmode.ToString(), CoordinationModes.TestAndPlaymode.ToString() };

    protected void CreateGUI()
    {
        if (Editors.IsAdditional()) return;

        InitializeVisibleMemory();
        sProjectSettingsInMemory = ProjectSettings.LoadInstance();

        var existingDefines = sProjectSettingsInMemory.scriptingDefineSymbols;
        var existingCommandLineParams = sProjectSettingsInMemory.commandlineParams;
        for (var i = 0; i < MaximumAmountOfEditors && i < existingDefines.Length; i++) {
            if (string.IsNullOrWhiteSpace(existingDefines[i])) continue;

            UnityEngine.Debug.Log($"[{i}] Using scripting define '{existingDefines[i]}'");
            sVisible.ScriptingDefineSymbols[i] = existingDefines[i];
            sVisible.PreviousScriptingDefineSymbols[i] = existingDefines[i];
        }
        for (var i = 0; i < MaximumAmountOfEditors && i < existingCommandLineParams.Length; i++) {
            if (string.IsNullOrWhiteSpace(existingCommandLineParams[i])) continue;

            UnityEngine.Debug.Log($"[{i}] Using command line param  '{existingCommandLineParams[i]}'");
            sVisible.CommandLineParams[i] = existingCommandLineParams[i];
        }
        for (var i = 0; i < MaximumAmountOfEditors; i++) sVisible.IsShowFoldout[i] = true;

        EditorApplication.playModeStateChanged += OriginalCoordinatePlaymodeStateChanged; // Duplicated from Editors for convenience (its more code to make this a singleton simply to bypass this)
    }

    private static void InitializeVisibleMemory()
    {
        sVisible.CoordinationMode = (CoordinationModes)EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal;
        sVisible.ScriptingDefineSymbols = new string[MaximumAmountOfEditors];
        sVisible.PreviousScriptingDefineSymbols = new string[MaximumAmountOfEditors];
        sVisible.CommandLineParams = new string[MaximumAmountOfEditors];
        sVisible.IsShowFoldout = new bool[MaximumAmountOfEditors];
        sVisible.IsSymlinked = new bool[MaximumAmountOfEditors];
        sSelectedOption = EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal;
    }

    protected void OnGUI()
    {
        var events = new Events();
        sPreviousSelection = sSelectedOption;

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
            sVisible.Path = Editors.GetEditorsAvailable();

            if (sVisible.IsSymlinked == null) {
                InitializeVisibleMemory();
            }
            for (var i = 0; i < sVisible.Path.Length; i++) {
                sVisible.IsSymlinked[i] = Editors.IsSymlinked(sVisible.Path[i]);
            }
        }

        /*- UI -*/
        GUILayout.BeginHorizontal("box");
        events.Settings = GUILayout.Button("Settings");
        events.Github = GUILayout.Button("Github");
        GUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        if (Editors.IsAdditional()) {
            EditorGUILayout.HelpBox("You can only launch additional editors from the original editor.", MessageType.Info);
        } else {
            if (sVisible.Path != null && sVisible.Path.Length >= 2) {
                GUILayout.BeginVertical();
                {
                    GUILayout.Space(20);
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Current Coordination Mode", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    sSelectedOption = GUILayout.SelectionGrid(sSelectedOption, Options, Options.Length);
                    if (sSelectedOption != sPreviousSelection) events.UpdateCoordinatePlay = true;
                    GUILayout.Space(10);

                    if (sVisible.CoordinationMode == CoordinationModes.TestAndPlaymode) {
                        var testState = (TestStates)UntilExitSettings.Coordinator_TestState;
                        using (new EditorGUILayout.HorizontalScope("box")) {
                            if (testState == TestStates.Off) {
                                GUI.color = Color.green;
                                events.StartTests = GUILayout.Button("Start Tests", GUILayout.Width(200));
                                GUI.color = Color.white;
                            } else {
                                GUI.color = Color.red;
                                events.StopTests = GUILayout.Button("Stop Tests", GUILayout.Width(200));
                                GUI.color = Color.white;
                            }
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            EditorGUI.LabelField(EditorGUILayout.GetControlRect(GUILayout.Width(50)), "Status:");
                            EditorGUILayout.HelpBox($"{testState}", MessageType.None, true);
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.Space(10);
                    }

                    sVisible.ScrollPosition = EditorGUILayout.BeginScrollView(sVisible.ScrollPosition);

                    for (var i = 0; i < sVisible.Path.Length; i++) {
                        var editor = sVisible.Path[i];
                        var editorInfo = EditorPaths.PopulateEditorInfo(editor);
                        var isProcessRunningForProject = false;
                        foreach (var p in sVisible.PathToProcessIds) {
                            if (p.path == editorInfo.Path) {
                                isProcessRunningForProject = true;
                                break;
                            }
                        }
                        GUILayout.BeginVertical("GroupBox");

                        events.Index = i;
                        using (new EditorGUILayout.HorizontalScope()) {
                            sVisible.IsShowFoldout[i] = EditorGUILayout.Foldout(sVisible.IsShowFoldout[i], string.Empty, true);
                            if (isProcessRunningForProject) {
                                GUILayout.Label($"{editorInfo.Name} [Open]", EditorStyles.boldLabel);
                            } else {
                                GUILayout.Label(editorInfo.Name);
                            }
                            GUILayout.FlexibleSpace();
                        }
                        if (sVisible.IsShowFoldout[i]) {
                            var editorType = sVisible.IsSymlinked[i] ? EditorType.Symlink : EditorType.HardCopy;
                            if (i != 0) EditorGUILayout.HelpBox($"{editorType}", MessageType.Info);
                            GUILayout.BeginHorizontal();
                            EditorGUILayout.TextField("Editor path", editorInfo.Path, EditorStyles.textField);
                            events.ShowInFinder = GUILayout.Button("Open in Finder", GUILayout.Width(170)) ? editorInfo.Path : events.ShowInFinder;
                            GUILayout.EndHorizontal();

                            if (i != 0) {
                                EditorGUI.BeginDisabledGroup(isProcessRunningForProject);
                                EditorGUILayout.LabelField("Command Line Params");
                                sVisible.CommandLineParams[i] = EditorGUILayout.TextField(sVisible.CommandLineParams[i], EditorStyles.textField);
                                EditorGUI.EndDisabledGroup();

                                if (sVisible.CoordinationMode != CoordinationModes.Standalone) {
                                    EditorGUILayout.LabelField("Scripting Define Symbols on Play [';' separated] (Note: This Overwrites! We will improve this in the future)");
                                    GUILayout.BeginHorizontal();
                                    sVisible.ScriptingDefineSymbols[i] = EditorGUILayout.TextField(sVisible.ScriptingDefineSymbols[i], EditorStyles.textField);
                                    GUILayout.EndHorizontal();
                                }

                                GUILayout.Space(10);
                                GUILayout.BeginHorizontal();
                                EditorGUI.BeginDisabledGroup(isProcessRunningForProject);
                                events.EditorOpen = GUILayout.Button("Open Editor") ? editorInfo.Path : events.EditorOpen;
                                EditorGUI.EndDisabledGroup();
                                EditorGUI.BeginDisabledGroup(!isProcessRunningForProject);
                                events.EditorClose = GUILayout.Button("Close Editor") ? editorInfo.Path : events.EditorClose;
                                EditorGUI.EndDisabledGroup();
                                GUILayout.EndHorizontal();
                                var customButtonStyle = new GUIStyle(GUI.skin.button)
                                {
                                    normal = { background = CreateColorTexture(new Color(0.2f, 0.2f, 0.2f)), textColor = Color.white },
                                    active = { background = CreateColorTexture(new Color(0.1f, 0.1f, 0.1f)), textColor = Color.white },
                                    hover = { textColor = Color.white },
                                    fontSize = 12,
                                    padding = new RectOffset(10, 10, 5, 5),
                                    margin = new RectOffset(2, 2, 2, 2),
                                };
                                var originalColor = GUI.backgroundColor;
                                GUI.backgroundColor = Red;
                                if (GUILayout.Button("Delete Editor", customButtonStyle)) {
                                    events.EditorDelete = EditorUtility.DisplayDialog(
                                        "Delete this editor?",
                                        "Are you sure you want to delete this editor?",
                                        "Delete",
                                        "Cancel") ? editorInfo.Path : events.EditorDelete;
                                }
                                GUI.backgroundColor = originalColor;
                            }
                        }
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();
                }
                GUILayout.EndVertical();
            } else {
                EditorGUILayout.HelpBox("Nothing to coordinate with. No additional editors are available yet.", MessageType.Info);
            }

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            events.EditorAdd = (sVisible.Path.Length < MaximumAmountOfEditors) && GUILayout.Button($"Add a {EditorType.Symlink} Editor") ? EditorType.Symlink : events.EditorAdd;
            events.EditorAdd = (sVisible.Path.Length < MaximumAmountOfEditors) && GUILayout.Button($"Add a {EditorType.HardCopy} Editor") ? EditorType.HardCopy : events.EditorAdd;
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            events.ShowInFinder = GUILayout.Button("Show Editors in Finder") ? Paths.ProjectRootPath : events.ShowInFinder;
            GUILayout.EndVertical();
        }

        /*- Handle Events -*/
        if (events.Github) {
            Application.OpenURL("https://github.com/hopeforsenegal/com.moonlitstudios.coordinator");
        }
        if (events.Settings) {
            SettingsService.OpenProjectSettings(CoordinatorSettingsProvider.MenuLocationInProjectSettings);
        }
        if (events.UpdateCoordinatePlay) {
            sVisible.CoordinationMode = (CoordinationModes)sSelectedOption;
            EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal = sSelectedOption;
        }
        if (events.StartTests) {
            EditorApplication.isPlaying = true;
            UntilExitSettings.Coordinator_TestState = (int)TestStates.Testing;
        }
        if (events.StopTests) {
            EditorApplication.isPlaying = false;
            UntilExitSettings.Coordinator_TestState = (int)TestStates.Off;
        }
        if (events.EditorAdd != default) {
            var original = EditorPaths.PopulateEditorInfo(Paths.ProjectPath);
            var additional = EditorPaths.PopulateEditorInfo($"{Paths.ProjectPath}Copy");

            Directory.CreateDirectory(additional.Path);
            if (events.EditorAdd == EditorType.Symlink) {
                Editors.Symlink(original.Assets, additional.Assets);
                Editors.Symlink(original.ProjectSettings, additional.ProjectSettings);
                Editors.Symlink(original.Packages, additional.Packages);
                Editors.MarkAsSymlink(additional.Path);
            } else {
                Editors.Hardcopy(original.Assets, additional.Assets);
                Editors.Hardcopy(original.ProjectSettings, additional.ProjectSettings);
                Editors.Hardcopy(original.Packages, additional.Packages);
            }
        }
        if (!string.IsNullOrWhiteSpace(events.EditorOpen)) {
            UnityEngine.Debug.Assert(Directory.Exists(events.EditorOpen), "No Editor at location");
            var process = Process.Start($"{EditorApplication.applicationPath}/Contents/MacOS/Unity", $"-projectPath \"{events.EditorOpen}\" {CommandLineParams.AdditionalEditorParams} {sVisible.CommandLineParams[events.Index]}");
            var processIds = new List<PathToProcessId>(sVisible.PathToProcessIds);
            processIds.Add(new PathToProcessId { path = events.EditorOpen, processID = process.Id });
            UntilExitSettings.Coordinator_ProjectPathToChildProcessID = PathToProcessId.Join(processIds.ToArray());
            sVisible.PathToProcessIds = processIds.ToArray();
            SaveProjectSettings();
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

    protected void OnLostFocus()
    {
        if (Editors.IsAdditional()) return;
        SaveProjectSettings();
    }

    private static void OriginalCoordinatePlaymodeStateChanged(PlayModeStateChange playmodeState)
    {
        if (playmodeState != PlayModeStateChange.ExitingEditMode) return;
        SaveProjectSettings();
    }

    private static void SaveProjectSettings()
    {
        if (sVisible.ScriptingDefineSymbols == null) return;

        var scriptingDefineCounts = 0;
        foreach (var item in sVisible.ScriptingDefineSymbols) {
            if (!string.IsNullOrWhiteSpace(item)) {
                scriptingDefineCounts++;
            }
        }
        var commandLineParamCounts = 0;
        foreach (var item in sVisible.CommandLineParams) {
            if (!string.IsNullOrWhiteSpace(item)) {
                commandLineParamCounts++;
            }
        }
        UnityEngine.Debug.Log($"Saving scripting {scriptingDefineCounts} define(s) and {commandLineParamCounts} command line param(s)");
        sProjectSettingsInMemory.scriptingDefineSymbols = sVisible.ScriptingDefineSymbols;
        sProjectSettingsInMemory.commandlineParams = sVisible.CommandLineParams;
        EditorUtility.SetDirty(sProjectSettingsInMemory);
        AssetDatabase.SaveAssetIfDirty(sProjectSettingsInMemory);
    }

    private static Texture2D CreateColorTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}