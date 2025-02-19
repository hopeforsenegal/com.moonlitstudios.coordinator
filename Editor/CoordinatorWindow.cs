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
    private const string Browse = "Open in Finder...";
    private const string ShowAllInDirectory = "Show Editors in Finder...";
#else
    private const string Browse = "Browse...";
    private const string ShowAllInDirectory = "Show Editors in File Explorer...";
#endif

    private struct Visible
    {
        public Vector2 ScrollPosition;
        public float RefreshInterval;
        public PathToProcessId[] PathToProcessIds;
        public string GlobalScriptingDefineSymbols;
        // NOTE: We are Struct of arrays (instead of Array of structs). This is to ensure our compatibility of the string arrays across domains (ex. ProjectSettings)
        public string[] ScriptingDefineSymbols;
        public string[] PreviousScriptingDefineSymbols;
        public string[] CommandLineParams;
        public string[] Path;
        public bool[] IsShowFoldout;
        public bool[] IsSymlinked;
        public bool IsShowFoldoutNew;
        public bool IsDirty;
        public int NumberOfProcessRunning;
        public int SelectedIndex;
        public bool PlaymodeWillEnd;
        public bool AfterPlaymodeEnded;
        internal int NumAttributeMethods;
    }

    public enum EventType { Open = 1, Close, }
    private struct Events
    {
        public EditorType EditorAdd;
        public EventType Editor;
        public int[] Index;
        public string[] Paths;
        public string EditorDelete;
        public string BrowseFolder;
        public bool UpdateCoordinatePlay;
        public bool Settings;
        public bool Github;
        public bool HasClickedToggle;
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
    private static readonly Color CoolGray = new Color(200 / 255f, 200 / 255f, 200 / 255f);
    private static Visible sVisible;
    private static ProjectSettings sProjectSettingsInMemory;
    private static GUIStyle sRadioButtonStyleBlue;
    private static GUIStyle sRadioButtonStyleGreen;
    private static GUIStyle sLabelStyle;
    private static Texture2D sColorTextureA;
    private static Texture2D sColorTextureB;

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

        sVisible.NumAttributeMethods = Editors.AfterPlayMethods().Length;

        EditorApplication.playModeStateChanged += OriginalCoordinatePlaymodeStateChanged; // Duplicated from Editors for convenience (its more code to make this a singleton simply to bypass this)
    }

    protected void OnDisable()
    {
        DestroyColorTexture(ref sColorTextureA);
        DestroyColorTexture(ref sColorTextureB);
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

        if (sRadioButtonStyleBlue == null || sRadioButtonStyleBlue.normal.background == null) {
            InitializeStyles();
        }

        /*- UI -*/
        if (Editors.IsAdditional()) {
            EditorGUILayout.HelpBox("You can only launch additional editors from the original editor.", MessageType.Info);
        } else {
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            if (sVisible.Path != null && sVisible.Path.Length >= 1) {
                GUILayout.BeginVertical();
                {
                    var previousSelection = sVisible.SelectedIndex;
                    var isToggled = RenderCoordinationMode(ref events);
                    if (isToggled != previousSelection) events.UpdateCoordinatePlay = true;
                    GUILayout.Space(5);
                    EditorGUI.LabelField(EditorGUILayout.GetControlRect(GUILayout.Width(50)), "Status:", EditorStyles.boldLabel);

                    var anEditorOpenMessage = $"{sVisible.NumberOfProcessRunning} Additional Editor(s) are Open. (switching modes not available until editors are close)";
                    if (EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal == 1) anEditorOpenMessage = $"{sVisible.NumberOfProcessRunning} Additional Editor(s) are Open and ready for Playmode. (Switching modes not available until editors are close)";
                    if (EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal == 1 && EditorApplication.isPlaying) anEditorOpenMessage = "All Editors are in Playmode";
                    if (UntilExitSettings.Coordinator_IsRunningAfterPlaymodeEnded) anEditorOpenMessage = "Running Post Test methods";
                    var statusMessage = UntilExitSettings.Coordinator_TestState switch { EditorStates.AllEditorsClosed => "No Additional Editors are Open", EditorStates.AnEditorsOpen => anEditorOpenMessage };
                    if (EditorUtility.scriptCompilationFailed) statusMessage = "Compilation errors detected! Unable to go into Playmode or run Tests!";
                    EditorGUILayout.HelpBox(statusMessage, EditorUtility.scriptCompilationFailed ? MessageType.Error : MessageType.None, true);

                    GUILayout.Space(10);
                    GUILayout.Label("Main:", EditorStyles.boldLabel);
                    {
                        var editor = sVisible.Path[0];
                        var editorInfo = EditorPaths.PopulateEditorInfo(editor);
                        GUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(true);
                        GUILayout.Space(10);
                        EditorGUILayout.TextField("Editor path", editorInfo.Path, EditorStyles.textField);
                        EditorGUI.EndDisabledGroup();
                        events.BrowseFolder = GUILayout.Button(Browse, GUILayout.Width(170)) ? editorInfo.Path : events.BrowseFolder;
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.Space(10);
                    GUILayout.Label("Additionals:", EditorStyles.boldLabel);
                    sVisible.ScrollPosition = EditorGUILayout.BeginScrollView(sVisible.ScrollPosition);

                    sVisible.NumberOfProcessRunning = 0;
                    for (var i = 1; i < sVisible.Path.Length; i++) {
                        var editor = sVisible.Path[i];
                        var editorInfo = EditorPaths.PopulateEditorInfo(editor);
                        var isProcessRunningForProject = false;
                        var editorType = sVisible.IsSymlinked[i] ? EditorType.Symlink : EditorType.HardCopy;
                        foreach (var p in sVisible.PathToProcessIds) {
                            if (p.Path == editorInfo.Path) {
                                isProcessRunningForProject = true;
                                sVisible.NumberOfProcessRunning += 1;
                                break;
                            }
                        }
                        GUILayout.BeginVertical("GroupBox");

                        using (new EditorGUILayout.HorizontalScope()) {
                            sVisible.IsShowFoldout[i] = EditorGUILayout.Foldout(sVisible.IsShowFoldout[i], string.Empty, true);
                            var status = $"[{editorType}]";
                            if (isProcessRunningForProject) status = $"[{editorType}|Open]";

                            if (isProcessRunningForProject) {
                                GUILayout.Label($"{status} {editorInfo.Name}", EditorStyles.boldLabel);
                            } else {
                                GUILayout.Label($"{status} {editorInfo.Name}");
                            }
                            GUILayout.FlexibleSpace();
                            if (i != 0) {
                                using (new BackgroundColorScope(!isProcessRunningForProject ? OpenGreen : Color.red)) {
                                    if (!isProcessRunningForProject) {
                                        if (GUILayout.Button("Open Editor", GUILayout.Width(180), GUILayout.Height(30))) {
                                            events.Editor = EventType.Open;
                                            events.Paths = new string[] { editorInfo.Path };
                                            events.Index = new int[] { i };
                                        }
                                    } else {
                                        if (GUILayout.Button("Close Editor", GUILayout.Width(180), GUILayout.Height(30))) {
                                            events.Editor = EventType.Close;
                                            events.Paths = new string[] { editorInfo.Path };
                                            events.Index = new int[] { i };
                                        }
                                    }
                                }
                            }
                        }
                        if (sVisible.IsShowFoldout[i]) {
                            GUILayout.Space(10);

                            GUILayout.BeginHorizontal();
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.TextField("Editor path", editorInfo.Path, EditorStyles.textField);
                            EditorGUI.EndDisabledGroup();
                            if (GUILayout.Button(Browse, GUILayout.Width(170))) {
                                events.BrowseFolder = editorInfo.Path;
                                events.Index = new int[] { i };
                            }
                            GUILayout.EndHorizontal();

                            EditorGUI.BeginDisabledGroup(isProcessRunningForProject);
                            EditorGUILayout.LabelField("Command Line Params");
                            sVisible.CommandLineParams[i] = EditorGUILayout.TextField(sVisible.CommandLineParams[i], EditorStyles.textField);

                            if (sVisible.SelectedIndex == 1) {
                                EditorGUILayout.LabelField("Scripting Define Symbols on Play [';' separated] (Note: This Overwrites! We will improve this in the future)");
                                GUILayout.BeginHorizontal();
                                var previousScriptingDefineSymbols = sVisible.ScriptingDefineSymbols[i];
                                sVisible.ScriptingDefineSymbols[i] = EditorGUILayout.TextField(sVisible.ScriptingDefineSymbols[i], EditorStyles.textField);
                                if (previousScriptingDefineSymbols != sVisible.ScriptingDefineSymbols[i]) {
                                    sVisible.IsDirty = true;
                                }

                                GUILayout.EndHorizontal();
                            }

                            GUILayout.Space(10);

                            GUILayout.BeginHorizontal();
                            var deleteButtonStyle = new GUIStyle(GUI.skin.button)
                            {
                                normal = { background = CreateColorTexture(ref sColorTextureA, new Color(0.2f, 0.2f, 0.2f)), textColor = Color.white },
                                active = { background = CreateColorTexture(ref sColorTextureB, new Color(0.1f, 0.1f, 0.1f)), textColor = Color.white },
                                hover = { textColor = Color.white },
                                fontSize = 12,
                                padding = new RectOffset(10, 10, 5, 5),
                                margin = new RectOffset(2, 2, 2, 2),
                            };
                            using (new BackgroundColorScope(DeleteRed)) {
                                if (GUILayout.Button("Delete Editor", deleteButtonStyle)) {
                                    var message = editorType == EditorType.Symlink ? "Are you sure you want to delete this editor?" : "Are you sure you want to delete this editor? All files will be permanently lost!";
                                    if (EditorUtility.DisplayDialog(
                                        "Delete this editor?",
                                        message,
                                        "Delete",
                                        "Cancel")) {
                                        events.EditorDelete = editorInfo.Path;
                                        events.Index = new int[] { i };
                                    }
                                }
                            }

                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            EditorGUI.EndDisabledGroup();
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

                    GUILayout.BeginHorizontal();
                    if (sVisible.Path.Length > 1) {
                        GUILayout.FlexibleSpace();
                        EventType eventType = default;
                        using (new BackgroundColorScope(OpenGreen)) {
                            if (GUILayout.Button("Open All Editors")) eventType = EventType.Open;
                        }
                        using (new BackgroundColorScope(Color.red)) {
                            if (GUILayout.Button("Close All Editors")) eventType = EventType.Close;
                        }
                        if (eventType == EventType.Open || eventType == EventType.Close) {
                            events.Editor = eventType;
                            var paths = new List<string>();
                            var indices = new List<int>();
                            for (int a = 1; a < sVisible.Path.Length; a++) {
                                paths.Add(sVisible.Path[a]);
                                indices.Add(a);
                            }
                            events.Paths = paths.ToArray();
                            events.Index = indices.ToArray();
                        }
                    }
                    GUILayout.EndHorizontal();

                    EditorGUILayout.EndScrollView();
                }
                GUILayout.EndVertical();
            } else {
                EditorGUILayout.HelpBox("Nothing to coordinate with. No additional editors are available yet.", MessageType.Info);
            }

            if (sVisible.Path != null && sVisible.Path.Length >= 1 && sVisible.SelectedIndex == 1) {
                var testState = UntilExitSettings.Coordinator_TestState;
                var hasAppearTestable = testState == EditorStates.AnEditorsOpen || testState == EditorStates.AllEditorsClosed;
                using (new EditorGUILayout.VerticalScope("box")) {
                    using (new EnableGroupScope(!EditorUtility.scriptCompilationFailed && !EditorApplication.isPlaying))
                    using (new EditorGUILayout.VerticalScope())
                    using (new BackgroundColorScope(hasAppearTestable ? TestBlue : Color.red)) {
                        if (hasAppearTestable) {
                            var previous = sVisible.PlaymodeWillEnd;
                            sVisible.PlaymodeWillEnd = GUILayout.Toggle(sVisible.PlaymodeWillEnd, "PlaymodeWillEnd        | (Invoke Action PlaymodeWillEnd.Invoke() right before we exit playmode so that a user might verify the game's state, ex. 10 goals)", GUILayout.Width(900));
                            if (previous != sVisible.PlaymodeWillEnd) events.HasClickedToggle = true;

                            previous = sVisible.AfterPlaymodeEnded;
                            sVisible.AfterPlaymodeEnded = GUILayout.Toggle(sVisible.AfterPlaymodeEnded, $"AfterPlaymodeEnded | (Run Attribute [AfterPlaymodeEnded] on {sVisible.NumAttributeMethods} method(s) after leaving playmode so that a user might upload to a server/create a build/etc)", GUILayout.Width(900));
                            if (previous != sVisible.AfterPlaymodeEnded) events.HasClickedToggle = true;
                        }
                    }

                    EditorGUILayout.LabelField("Global Scripting Define Symbols on Play [';' separated] (Note: This Overwrites! We will improve this in the future)");
                    GUILayout.BeginHorizontal();
                    var previousScriptingDefineSymbols = sVisible.GlobalScriptingDefineSymbols;
                    sVisible.GlobalScriptingDefineSymbols = EditorGUILayout.TextField(sVisible.GlobalScriptingDefineSymbols, EditorStyles.textField);
                    if (previousScriptingDefineSymbols != sVisible.GlobalScriptingDefineSymbols) {
                        sVisible.IsDirty = true;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(10);
            }

            EditorGUI.EndDisabledGroup();
        }

        /*- Handle Events -*/
        if (events.Github) {
            Application.OpenURL("https://github.com/hopeforsenegal/com.moonlitstudios.coordinator");
        }
        if (events.Settings) {
            SettingsService.OpenProjectSettings(CoordinatorSettingsProvider.MenuLocationInProjectSettings);
        }
        if (events.UpdateCoordinatePlay) {
            sVisible.IsDirty = true;
            EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal = sVisible.SelectedIndex;
            // This might not actually fix the issue all the way (we could fix it potentially by switching to a dedicated file for the play setting)
            for (var i = 0; i < CoordinatorWindow.MaximumAmountOfEditors; i++) {
                SocketLayer.DeleteMessage($"{MessageEndpoint.Scene}{i}");
                SocketLayer.DeleteMessage($"{MessageEndpoint.Playmode}{i}");
            }
        }
        if (events.HasClickedToggle) {
            UntilExitSettings.Coordinator_PlaymodeWillEnd = sVisible.PlaymodeWillEnd;
            UntilExitSettings.Coordinator_AfterPlaymodeEnded = sVisible.AfterPlaymodeEnded;
        }
        if (events.EditorAdd != default) {
            sVisible.IsDirty = true;
            var next = sVisible.Path == null ? 0 : sVisible.Path.Length;
            var original = EditorPaths.PopulateEditorInfo(Paths.ProjectPath);
            var additional = EditorPaths.PopulateEditorInfo($"{Paths.ProjectPath}{Paths.AdditionalProjectSpecifier}{next}");

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
        if (events.Editor == EventType.Open) {
            sVisible.IsDirty = true;
            var processIds = new List<PathToProcessId>(sVisible.PathToProcessIds);
            for (int i = 0; i < events.Paths.Length; i++) {
                var path = events.Paths[i];
                var index = events.Index[i];
                UnityEngine.Debug.Assert(Directory.Exists(path), "No Editor at location");
                var editorInfo = EditorPaths.PopulateEditorInfo(path);
                var isProcessRunningForProject = false;
                foreach (var p in sVisible.PathToProcessIds) {
                    if (p.Path == editorInfo.Path) {
                        isProcessRunningForProject = true;
                        break;
                    }
                }
                if (!isProcessRunningForProject) {
#if UNITY_EDITOR_OSX
                    var process = Process.Start($"{EditorApplication.applicationPath}/Contents/MacOS/Unity", $"-projectPath \"{path}\" {CommandLineParams.BuildAdditionalEditorParams(index.ToString())} {sVisible.CommandLineParams[index]}");
#else
                    var process = Process.Start($"{EditorApplication.applicationPath}", $"-projectPath \"{path}\" {CommandLineParams.BuildAdditionalEditorParams(index.ToString())} {sVisible.CommandLineParams[index]}");
#endif
                    processIds.Add(new PathToProcessId { Path = path, ProcessID = process.Id });
                }
            }
            UntilExitSettings.Coordinator_ProjectPathToChildProcessID = PathToProcessId.Join(processIds.ToArray());
            UntilExitSettings.Coordinator_TestState = EditorStates.AnEditorsOpen;
            sVisible.PathToProcessIds = processIds.ToArray();
            SaveProjectSettings();
        }
        if (events.Editor == EventType.Close) {
            sVisible.IsDirty = true;
            var pathToProcessIds = sVisible.PathToProcessIds;
            var hasKilled = false;
            foreach (var p in pathToProcessIds) {
                foreach (var pp in events.Paths) {
                    if (p.Path == pp) {
                        try { Process.GetProcessById(p.ProcessID).Kill(); } // Is calling Kill() twice bad? Probably not so we don't need to update local memory
                        catch (InvalidOperationException) { }
                        hasKilled = true;
                        break;
                    }
                }
            }

            UntilExitSettings.Coordinator_TestState = sVisible.NumberOfProcessRunning == 1 && hasKilled ? EditorStates.AllEditorsClosed : EditorStates.AnEditorsOpen;
        }
        if (!string.IsNullOrWhiteSpace(events.EditorDelete)) {
            sVisible.IsDirty = true;
#if UNITY_EDITOR_OSX
            FileUtil.DeleteFileOrDirectory(events.EditorDelete);
#else
            Process.Start("cmd.exe", $"/c rmdir /s/q \"{events.EditorDelete}\"");
#endif
        }
        if (!string.IsNullOrWhiteSpace(events.BrowseFolder)) {
            sVisible.IsDirty = true;
            UnityEngine.Debug.Assert(Directory.Exists(events.BrowseFolder), "Not a valid location");
            Process.Start(events.BrowseFolder);
        }
    }

    protected void OnLostFocus()
    {
        if (Editors.IsAdditional()) return;
        if (!sVisible.IsDirty) return;
        sVisible.IsDirty = false;
        SaveProjectSettings();
    }

    private static void InitializeVisibleMemory()
    {
        sVisible.ScriptingDefineSymbols = new string[MaximumAmountOfEditors];
        sVisible.PreviousScriptingDefineSymbols = new string[MaximumAmountOfEditors];
        sVisible.CommandLineParams = new string[MaximumAmountOfEditors];
        sVisible.IsSymlinked = new bool[MaximumAmountOfEditors];
        sVisible.IsShowFoldout = new bool[MaximumAmountOfEditors];
        sVisible.IsShowFoldoutNew = true;
        sVisible.SelectedIndex = EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal;
    }

    private static void InitializeStyles()
    {
        sRadioButtonStyleBlue = new GUIStyle();
        sRadioButtonStyleBlue.normal.background = CreateRadioButtonTexture(16, new Color(0.3f, 0.3f, 0.3f), Color.gray);
        sRadioButtonStyleBlue.onNormal.background = CreateRadioButtonTexture(16, new Color(0.3f, 0.3f, 0.3f), new Color(0.2f, 0.6f, 0.9f));
        sRadioButtonStyleGreen = new GUIStyle();
        sRadioButtonStyleGreen.normal.background = CreateRadioButtonTexture(16, new Color(0.3f, 0.3f, 0.3f), Color.gray);
        sRadioButtonStyleGreen.onNormal.background = CreateRadioButtonTexture(16, new Color(0.3f, 0.3f, 0.3f), new Color(0.2f, 0.9f, 0.6f));

        sLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = CoolGray } };
    }

    private static Rect RadioButton(int index, ref int selectedIndex, string name)
    {
        var controlRect = EditorGUILayout.BeginHorizontal();
        var isSelected = selectedIndex == index;
        if (GUILayout.Toggle(isSelected, "", index == 0 ? sRadioButtonStyleBlue : sRadioButtonStyleGreen, GUILayout.Width(20), GUILayout.Height(20))) {
            selectedIndex = index;
        }
        GUILayout.Space(10);

        GUILayout.BeginVertical();
        GUILayout.Space(-1);
        GUILayout.Label(name, sLabelStyle);
        GUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        return controlRect;
    }

    private static bool IconButton(string iconPath, out Rect rect)
    {
        var isClicked = GUILayout.Button(EditorGUIUtility.IconContent(iconPath), GUIStyle.none);
        rect = GUILayoutUtility.GetLastRect();
        return isClicked;
    }

    private static int RenderCoordinationMode(ref Events events)
    {
        GUILayout.BeginHorizontal("box");
        GUILayout.FlexibleSpace();

        using (new EnableGroupScope(sVisible.NumberOfProcessRunning == 0)) {
            GUILayout.BeginVertical("groupbox", GUILayout.Width(400));

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            events.Settings = IconButton("_Popup@2x", out var settingsRect);
            events.Github = IconButton("_Help@2x", out var githubRect);
            // Tool tips
            if (settingsRect.Contains(Event.current.mousePosition)) GUI.Label(new Rect(Event.current.mousePosition.x + 20, Event.current.mousePosition.y + 10, 200, 20), "Settings", EditorStyles.helpBox);
            if (githubRect.Contains(Event.current.mousePosition)) GUI.Label(new Rect(Event.current.mousePosition.x + 20, Event.current.mousePosition.y + 10, 200, 20), "Help/Documentation", EditorStyles.helpBox);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Coordination Mode:", EditorStyles.boldLabel);
            GUILayout.Space(20);

            GUILayout.BeginVertical();
            GUILayout.Space(20);
            var rectTop = RadioButton(0, ref sVisible.SelectedIndex, "No Coordination");
            var rectBottom = RadioButton(1, ref sVisible.SelectedIndex, "Coordinate Editors");
            // Tool tips
            if (rectTop.Contains(Event.current.mousePosition)) GUI.Label(new Rect(Event.current.mousePosition.x + 20, Event.current.mousePosition.y - 50, 400, 40), "Interact with your editors manually as if you created them and opened them yourself", EditorStyles.helpBox);
            if (rectBottom.Contains(Event.current.mousePosition)) GUI.Label(new Rect(Event.current.mousePosition.x + 20, Event.current.mousePosition.y + 10, 400, 40), "Your additional editors will go into playmode when the original main editor goes into playmode", EditorStyles.helpBox);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return sVisible.SelectedIndex;
    }

    private static Texture2D CreateColorTexture(ref Texture2D colorTexture, Color color)
    {
        if (colorTexture == null) colorTexture = new Texture2D(1, 1);

        colorTexture.SetPixel(0, 0, color);
        colorTexture.Apply();
        return colorTexture;
    }

    private static void DestroyColorTexture(ref Texture2D colorTexture)
    {
        if (colorTexture != null) {
            DestroyImmediate(colorTexture);
            colorTexture = null;
        }
    }

    private static Texture2D CreateRadioButtonTexture(int size, Color outlineColor, Color fillColor)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA64, false) { filterMode = FilterMode.Bilinear };
        var center = size / 2f;
        var outerRadius = size / 2f - 1f;
        var innerRadius = outerRadius - 2f; // 2px thick outline

        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                var distanceFromCenter = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));

                if (distanceFromCenter <= outerRadius) {
                    var t = Mathf.Clamp01((distanceFromCenter - innerRadius) / 2f);
                    var pixelColor = distanceFromCenter <= innerRadius
                                        ? fillColor
                                        : Color.Lerp(fillColor, outlineColor, t);
                    var alpha = 1f - Mathf.Clamp01(distanceFromCenter - outerRadius); // Apply anti-aliasing to the edges
                    pixelColor.a *= alpha;

                    texture.SetPixel(x, y, pixelColor);
                } else {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        return texture;
    }

    private static void OriginalCoordinatePlaymodeStateChanged(PlayModeStateChange playmodeState)
    {
        if (playmodeState != PlayModeStateChange.ExitingEditMode) return;
        SaveProjectSettings();
    }

    private static void SaveProjectSettings()
    {
        if (sVisible.ScriptingDefineSymbols == null) return;
        if (sProjectSettingsInMemory == null) return;

        var scriptingDefineCounts = 0;
        foreach (var item in sVisible.ScriptingDefineSymbols) {
            if (string.IsNullOrWhiteSpace(item)) continue;
            scriptingDefineCounts++; break;
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
}