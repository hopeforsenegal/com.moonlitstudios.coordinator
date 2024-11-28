using System;
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

#if UNITY_EDITOR_OSX
    public const string Browse = "Open in Finder...";
    public const string ShowAllInDirectory = "Show Editors in Finder...";
#else
    public const string Browse = "Browse...";
    public const string ShowAllInDirectory = "Show Editors Directory...";
#endif

    private struct Visible
    {
        public Vector2 ScrollPosition;
        public int SelectedOption;
        public float RefreshInterval;
        public CoordinationModes CoordinationMode;
        public PathToProcessId[] PathToProcessIds;
        public string GlobalScriptingDefineSymbols;
        // NOTE: We are Struct of arrays (instead of Array of structs). This is to ensure our compatibility of the string arrays across domains (ex. ProjectSettings)
        public string[] ScriptingDefineSymbols;
        public string[] PreviousScriptingDefineSymbols;
        public string[] CommandLineParams;
        public string[] Path;
        public bool[] IsShowFoldout;
        public bool[] IsSymlinked;
        internal bool IsShowFoldoutNew;
    }

    private struct Events
    {
        public int Index;
        public EditorType EditorAdd;
        public string EditorOpen;
        public string EditorClose;
        public string EditorDelete;
        public string BrowseFolder;
        public bool UpdateCoordinatePlay;
        public bool StartTests;
        public bool StopTests;
    }

    private class BackgroundColorScope : GUI.Scope
    {
        private readonly Color m_Color;
        public BackgroundColorScope(Color tempColor) { m_Color = GUI.backgroundColor; GUI.backgroundColor = tempColor; }
        protected override void CloseScope() => GUI.backgroundColor = m_Color;
    }

    private class EnableGroupScope : GUI.Scope
    {
        private readonly bool m_Enabled;
        public EnableGroupScope(bool enabled) { m_Enabled = GUI.enabled; GUI.enabled = enabled; }
        protected override void CloseScope() => GUI.enabled = m_Enabled;
    }

    public const int MaximumAmountOfEditors = 6;
    private static readonly Color DeleteRed = new Color(255 / 255f, 235 / 255f, 235 / 255f);
    private static readonly Color TestBlue = new Color(230 / 255f, 230 / 255f, 255 / 255f);
    private static readonly Color OpenGreen = new Color(230 / 255f, 255 / 255f, 230 / 255f);
    private static readonly string[] Options = { CoordinationModes.Standalone.ToString(), CoordinationModes.Playmode.ToString(), CoordinationModes.TestAndPlaymode.ToString() };
    private static Visible sVisible;
    private static ProjectSettings sProjectSettingsInMemory;

    protected void CreateGUI()
    {
        if (Editors.IsAdditional()) return;

        InitializeVisibleMemory();
        sProjectSettingsInMemory = ProjectSettings.LoadInstance();

        var existingDefines = sProjectSettingsInMemory.scriptingDefineSymbols;
        var existingCommandLineParams = sProjectSettingsInMemory.commandlineParams;
        sVisible.GlobalScriptingDefineSymbols = PlayerSettings.GetScriptingDefineSymbols(Editors.BuildTarget);
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
        sVisible.IsSymlinked = new bool[MaximumAmountOfEditors];
        sVisible.IsShowFoldout = new bool[MaximumAmountOfEditors];
        sVisible.IsShowFoldoutNew = true;
        sVisible.SelectedOption = EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal;
    }

    protected void OnGUI()
    {
        var events = new Events();
        var previousSelection = sVisible.SelectedOption;

        if (sVisible.RefreshInterval > 0) {
            sVisible.RefreshInterval -= Time.deltaTime;
        } else {
            sVisible.RefreshInterval = .5f; // Refresh every half second
            ////////////////////////
            var pathToProcessIds = PathToProcessId.Split(UntilExitSettings.Coordinator_ProjectPathToChildProcessID);
            var updatedListOfProcesses = new List<PathToProcessId>();
            foreach (var p in pathToProcessIds) {
                if (Editors.IsProcessAlive(p.ProcessID)) {
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
        if (Editors.IsAdditional()) {
            EditorGUILayout.HelpBox("You can only launch additional editors from the original editor.", MessageType.Info);
        } else {
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            if (sVisible.Path != null && sVisible.Path.Length >= 2) {
                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Current Coordination Mode", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    sVisible.SelectedOption = GUILayout.SelectionGrid(sVisible.SelectedOption, Options, Options.Length);
                    if (sVisible.SelectedOption != previousSelection) events.UpdateCoordinatePlay = true;
                    GUILayout.Space(20);

                    GUILayout.Label("Editors:", EditorStyles.boldLabel);
                    sVisible.ScrollPosition = EditorGUILayout.BeginScrollView(sVisible.ScrollPosition);

                    for (var i = 0; i < sVisible.Path.Length; i++) {
                        var editor = sVisible.Path[i];
                        var editorInfo = EditorPaths.PopulateEditorInfo(editor);
                        var isProcessRunningForProject = false;
                        foreach (var p in sVisible.PathToProcessIds) {
                            if (p.Path == editorInfo.Path) {
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
                            if (i != 0) {
                                using (new BackgroundColorScope(!isProcessRunningForProject ? OpenGreen : Color.red)) {
                                    if (!isProcessRunningForProject) {
                                        events.EditorOpen = GUILayout.Button("Open Editor", GUILayout.Width(180), GUILayout.Height(30)) ? editorInfo.Path : events.EditorOpen;
                                    } else {
                                        events.EditorClose = GUILayout.Button("Close Editor", GUILayout.Width(180), GUILayout.Height(30)) ? editorInfo.Path : events.EditorClose;
                                    }
                                }
                            }
                        }
                        if (sVisible.IsShowFoldout[i]) {
                            GUILayout.Space(10);
                            var editorType = sVisible.IsSymlinked[i] ? EditorType.Symlink : EditorType.HardCopy;
                            if (i != 0) EditorGUILayout.HelpBox($"{editorType}", MessageType.Info);
                            GUILayout.BeginHorizontal();
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.TextField("Editor path", editorInfo.Path, EditorStyles.textField);
                            EditorGUI.EndDisabledGroup();
                            events.BrowseFolder = GUILayout.Button(Browse, GUILayout.Width(170)) ? editorInfo.Path : events.BrowseFolder;
                            GUILayout.EndHorizontal();

                            if (i != 0) {
                                EditorGUI.BeginDisabledGroup(isProcessRunningForProject);
                                EditorGUILayout.LabelField("Command Line Params");
                                sVisible.CommandLineParams[i] = EditorGUILayout.TextField(sVisible.CommandLineParams[i], EditorStyles.textField);

                                if (sVisible.CoordinationMode != CoordinationModes.Standalone) {
                                    EditorGUILayout.LabelField("Scripting Define Symbols on Play [';' separated] (Note: This Overwrites! We will improve this in the future)");
                                    GUILayout.BeginHorizontal();
                                    sVisible.ScriptingDefineSymbols[i] = EditorGUILayout.TextField(sVisible.ScriptingDefineSymbols[i], EditorStyles.textField);
                                    GUILayout.EndHorizontal();
                                }

                                GUILayout.Space(10);

                                GUILayout.BeginHorizontal();
                                var customButtonStyle = new GUIStyle(GUI.skin.button)
                                {
                                    normal = { background = CreateColorTexture(new Color(0.2f, 0.2f, 0.2f)), textColor = Color.white },
                                    active = { background = CreateColorTexture(new Color(0.1f, 0.1f, 0.1f)), textColor = Color.white },
                                    hover = { textColor = Color.white },
                                    fontSize = 12,
                                    padding = new RectOffset(10, 10, 5, 5),
                                    margin = new RectOffset(2, 2, 2, 2),
                                };
                                using (new BackgroundColorScope(DeleteRed)) {
                                    if (GUILayout.Button("Delete Editor", customButtonStyle)) {
                                        events.EditorDelete = EditorUtility.DisplayDialog(
                                            "Delete this editor?",
                                            "Are you sure you want to delete this editor?",
                                            "Delete",
                                            "Cancel") ? editorInfo.Path : events.EditorDelete;
                                    }
                                }

                                GUILayout.FlexibleSpace();
                                GUILayout.EndHorizontal();
                                EditorGUI.EndDisabledGroup();
                            }
                        }
                        GUILayout.EndVertical();
                    }


                    GUILayout.BeginVertical("GroupBox");
                    using (new EditorGUILayout.HorizontalScope()) {
                        sVisible.IsShowFoldoutNew = EditorGUILayout.Foldout(sVisible.IsShowFoldoutNew, string.Empty, true);
                        GUILayout.Label("Additional Editor Options");
                        GUILayout.FlexibleSpace();
                    }
                    if (sVisible.IsShowFoldoutNew) {
                        GUILayout.BeginVertical("box");
                        events.BrowseFolder = GUILayout.Button(ShowAllInDirectory) ? Paths.ProjectRootPath : events.BrowseFolder;
                        GUILayout.BeginHorizontal();
                        events.EditorAdd = (sVisible.Path.Length < MaximumAmountOfEditors) && GUILayout.Button($"Add a {EditorType.Symlink} Editor") ? EditorType.Symlink : events.EditorAdd;
                        events.EditorAdd = (sVisible.Path.Length < MaximumAmountOfEditors) && GUILayout.Button($"Add a {EditorType.HardCopy} Editor") ? EditorType.HardCopy : events.EditorAdd;
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndVertical();

                    EditorGUILayout.EndScrollView();
                }
                GUILayout.EndVertical();
            } else {
                EditorGUILayout.HelpBox("Nothing to coordinate with. No additional editors are available yet.", MessageType.Info);
            }

            if (sVisible.CoordinationMode == CoordinationModes.TestAndPlaymode) {
                var testState = UntilExitSettings.Coordinator_TestState;
                using (new EditorGUILayout.VerticalScope("box")) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        using (new BackgroundColorScope(testState == TestStates.Off ? TestBlue : Color.red)) {
                            if (testState == TestStates.Off) {
                                events.StartTests = GUILayout.Button("Start Tests", GUILayout.Width(200));
                            } else {
                                using (new EnableGroupScope(true)) {
                                    events.StopTests = GUILayout.Button("Stop Tests", GUILayout.Width(200));
                                }
                            }
                        }
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        EditorGUI.LabelField(EditorGUILayout.GetControlRect(GUILayout.Width(50)), "Status:");
                        EditorGUILayout.HelpBox($"{testState}", MessageType.None, true);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    EditorGUILayout.LabelField("Global Scripting Define Symbols on Play [';' separated] (Note: This Overwrites! We will improve this in the future)");
                    GUILayout.BeginHorizontal();
                    sVisible.GlobalScriptingDefineSymbols = EditorGUILayout.TextField(sVisible.GlobalScriptingDefineSymbols, EditorStyles.textField);
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(10);
            }

            EditorGUI.EndDisabledGroup();
        }

        /*- Handle Events -*/
        if (events.UpdateCoordinatePlay) {
            sVisible.CoordinationMode = (CoordinationModes)sVisible.SelectedOption;
            EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal = sVisible.SelectedOption;
        }
        if (events.StartTests) {
            UntilExitSettings.Coordinator_TestState = TestStates.Testing;
            SaveProjectSettings();
            AssetDatabase.Refresh();

            EditorApplication.delayCall += () =>
            {
                UntilExitSettings.Coordinator_HasDelayEnterPlaymode = true;
            };
        }
        if (events.StopTests) {
            EditorApplication.isPlaying = false;
            UntilExitSettings.Coordinator_TestState = TestStates.Off;
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
                Editors.HardCopy(original.Assets, additional.Assets);
                Editors.HardCopy(original.ProjectSettings, additional.ProjectSettings);
                Editors.HardCopy(original.Packages, additional.Packages);
            }
        }
        if (!string.IsNullOrWhiteSpace(events.EditorOpen)) {
            UnityEngine.Debug.Assert(Directory.Exists(events.EditorOpen), "No Editor at location");
            var process = Process.Start($"{EditorApplication.applicationPath}/Contents/MacOS/Unity", $"-projectPath \"{events.EditorOpen}\" {CommandLineParams.BuildAdditionalEditorParams(events.Index.ToString())} {sVisible.CommandLineParams[events.Index]}");
            var processIds = new List<PathToProcessId>(sVisible.PathToProcessIds);
            processIds.Add(new PathToProcessId { Path = events.EditorOpen, ProcessID = process.Id });
            UntilExitSettings.Coordinator_ProjectPathToChildProcessID = PathToProcessId.Join(processIds.ToArray());
            sVisible.PathToProcessIds = processIds.ToArray();
            SaveProjectSettings();
        }
        if (!string.IsNullOrWhiteSpace(events.EditorClose)) {
            var pathToProcessIds = sVisible.PathToProcessIds;
            foreach (var p in pathToProcessIds) {
                if (p.Path == events.EditorClose) {
                    Process.GetProcessById(p.ProcessID).Kill(); // Is calling Kill() twice bad? Probably not so we don't need to update local memory
                    break;
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(events.EditorDelete)) {
            FileUtil.DeleteFileOrDirectory(events.EditorDelete);
        }
        if (!string.IsNullOrWhiteSpace(events.BrowseFolder)) {
            UnityEngine.Debug.Assert(Directory.Exists(events.BrowseFolder), "Not a valid location");
            Process.Start(events.BrowseFolder);
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
        UnityEngine.Debug.Log($"Saving scripting {scriptingDefineCounts} define(s) and {commandLineParamCounts} command line param(s) with '{PlayerSettings.GetScriptingDefineSymbols(Editors.BuildTarget)}' vs '{sVisible.GlobalScriptingDefineSymbols}'");
        sProjectSettingsInMemory.scriptingDefineSymbols = sVisible.ScriptingDefineSymbols;
        sProjectSettingsInMemory.commandlineParams = sVisible.CommandLineParams;

        foreach (var group in (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup))) {
            if (group == BuildTargetGroup.Unknown) continue;

            try { PlayerSettings.SetScriptingDefineSymbolsForGroup(group, sVisible.GlobalScriptingDefineSymbols); }
            catch (ArgumentException) { }
        }

        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        EditorUtility.SetDirty(sProjectSettingsInMemory);
    }

    private static Texture2D CreateColorTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}