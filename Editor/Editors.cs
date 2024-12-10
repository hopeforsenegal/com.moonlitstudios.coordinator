using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum EditorType { Symlink = 1, HardCopy }
internal enum EditorStates { AllEditorsClosed, AnEditorsOpen, EditorsPlaymode, RunningPostTest }
internal static class CommandLineParams
{
    public static string Additional { get; } = "--additional";
    public static string Original { get; } = "-original";
    private static string OriginalProcessID { get; } = $"{Original} {Process.GetCurrentProcess().Id}";
    public static string Port { get; } = "-port";
    private static string BuildPortParam(string port) => $"{Port} {port}";
    public static string ParsePort(string commandLine)
    {
        var index = commandLine.IndexOf(Port) + Port.Length;
        var remain = commandLine.Substring(index).Trim().Split(" ");
        return remain[0];
    }

    public static string BuildAdditionalEditorParams(string port) => string.Join(" ", Additional, OriginalProcessID, BuildPortParam(port));
}
internal static class Paths
{
    public static string ProjectPath { get; } = Application.dataPath.Replace("/Assets", "");
    public static string ProjectRootPath { get; } = Path.GetFullPath(Path.Combine(ProjectPath, ".."));
    public static string GetProjectName()
    {
        var s = Application.dataPath.Split('/');
        var projectName = s[s.Length - 2];
        return projectName;
    }
}
internal static class MessageEndpoint
{
    public static string Playmode { get; } = Path.Combine(Paths.ProjectRootPath, nameof(PlayModeStateChange));
    public static string Scene { get; } = Path.Combine(Paths.ProjectRootPath, nameof(UnityEngine.SceneManagement.Scene));
}
internal static class Messages
{
    public const string Edit = nameof(Edit);
    internal static string Play(string[] scriptingDefineSymbols) => $"{nameof(Play)}|{string.Join(":", scriptingDefineSymbols)}";
}
internal static class EditorUserSettings
{
    public static int Coordinator_CoordinatePlaySettingOnOriginal { get => EditorPrefs.GetInt(nameof(Coordinator_CoordinatePlaySettingOnOriginal), 0); set => EditorPrefs.SetInt(nameof(Coordinator_CoordinatePlaySettingOnOriginal), value); }
}
internal static class UntilExitSettings // SessionState is cleared when Unity exits. But survives domain reloads.
{
    public static EditorStates Coordinator_TestState { get => (EditorStates)SessionState.GetInt(nameof(Coordinator_TestState), 0); set => SessionState.SetInt(nameof(Coordinator_TestState), (int)value); }
    public static string Coordinator_ParentProcessID { get => SessionState.GetString(nameof(Coordinator_ParentProcessID), string.Empty); set => SessionState.SetString(nameof(Coordinator_ParentProcessID), value); }
    public static string Coordinator_ProjectPathToChildProcessID { get => SessionState.GetString(nameof(Coordinator_ProjectPathToChildProcessID), string.Empty); set => SessionState.SetString(nameof(Coordinator_ProjectPathToChildProcessID), value); }
    public static string Coordinator_CurrentGlobalScriptingDefines { get => SessionState.GetString(nameof(Coordinator_CurrentGlobalScriptingDefines), string.Empty); set => SessionState.SetString(nameof(Coordinator_CurrentGlobalScriptingDefines), value); }
    public static bool Coordinator_IsCoordinatePlayThisSessionOnAdditional { get => SessionState.GetInt(nameof(Coordinator_IsCoordinatePlayThisSessionOnAdditional), 0) == 1; set => SessionState.SetInt(nameof(Coordinator_IsCoordinatePlayThisSessionOnAdditional), value ? 1 : 0); }
    public static bool Coordinator_HasDelayEnterPlaymode { get => SessionState.GetInt(nameof(Coordinator_HasDelayEnterPlaymode), 0) == 1; set => SessionState.SetInt(nameof(Coordinator_HasDelayEnterPlaymode), value ? 1 : 0); }
    public static bool Coordinator_HasTestsSetToRun { get => SessionState.GetInt(nameof(Coordinator_HasTestsSetToRun), 0) == 1; set => SessionState.SetInt(nameof(Coordinator_HasTestsSetToRun), value ? 1 : 0); }
    public static bool Coordinator_IsRunPostTest { get => SessionState.GetInt(nameof(Coordinator_IsRunPostTest), 0) == 1; set => SessionState.SetInt(nameof(Coordinator_IsRunPostTest), value ? 1 : 0); }
}
internal class SessionStateConvenientListInt
{
    private readonly string m_Key;

    public SessionStateConvenientListInt(string key) => m_Key = key;
    private int[] Get() => SessionState.GetIntArray(m_Key, new int[] { });
    private void Clear() => SessionState.EraseIntArray(m_Key);
    public int Count() => Get().Length;

    public void Queue(int value) => SessionState.SetIntArray(m_Key, new List<int>(SessionState.GetIntArray(m_Key, new int[] { })) { value }.ToArray());// @value add
    public int Dequeue()
    {
        var array = Get();
        Clear();
        for (var i = 1; i < array.Length - 1; i++) Queue(array[i]);
        return array[0];
    }
}
internal struct PathToProcessId
{   // Format is 'long/project/path|1234124' and we store all of them separated by ;
    public string Path;
    public int ProcessID;
    private const string Separator = "|";
    private const string End = ";";

    public static string Join(params PathToProcessId[] pathToProcessIds)
    {
        var result = string.Empty;
        foreach (var p in pathToProcessIds) result += $"{p.Path}{Separator}{p.ProcessID}{End}";
        return result;
    }
    public static PathToProcessId[] Split(string toParse)
    {
        var pathToProcessIdSplit = toParse.Split(End);
        var result = new List<PathToProcessId>();
        foreach (var p in pathToProcessIdSplit) {
            if (string.IsNullOrWhiteSpace(p)) continue;

            var split = p.Split(Separator);
            if (int.TryParse(split[1], out var resultProcessId)) {
                result.Add(new PathToProcessId { Path = split[0], ProcessID = resultProcessId });
            } else {
                UnityEngine.Debug.LogWarning($"We failed to parse the {nameof(PathToProcessId)} on path '{split[0]}'");
            }
        }
        return result.ToArray();
    }
}
internal struct EditorPaths
{
    public string Name;
    public string Path;
    public string Assets;
    public string ProjectSettings;
    public string Packages;

    public static EditorPaths PopulateEditorInfo(string path)
    {
        var pathByFolders = path.Split('/');
        return new EditorPaths
        {
            Path = path,
            Name = pathByFolders[pathByFolders.Length - 1],
            Assets = $"{path}/{nameof(Assets)}",
            ProjectSettings = $"{path}/{nameof(ProjectSettings)}",
            Packages = $"{path}/{nameof(Packages)}",
        };
    }
}

[InitializeOnLoad] // Without being in a InitializeOnLoad, the EnteredPlaymode event will get dropped in OriginalCoordinatePlaymodeStateChanged. We also cannot put that functionality in a EditorWindow/CoordinatorWindow
public static class Editors
{
    private static readonly List<string> EndPointsToProcess = new List<string>();
    private static readonly SessionStateConvenientListInt Playmode = new SessionStateConvenientListInt(nameof(Playmode));
    private static float sRefreshInterval;
    public static NamedBuildTarget BuildTarget { get; } = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone);

    static Editors()
    {
        if (!IsAdditional()) {
            UnityEngine.Debug.Log($"Is Original. [{PlayerSettings.GetScriptingDefineSymbols(BuildTarget)}]");
            var path = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrWhiteSpace(path)) {
                UnityEngine.Debug.Assert(!string.IsNullOrWhiteSpace(SceneManager.GetActiveScene().name));
                for (var i = 0; i < CoordinatorWindow.MaximumAmountOfEditors; i++) {
                    SocketLayer.WriteMessage($"{MessageEndpoint.Scene}{i}", path);
                }
            }
            EditorApplication.playModeStateChanged += OriginalCoordinatePlaymodeStateChanged;
            EditorApplication.update += OriginalUpdate;
        } else {
            var port = CommandLineParams.ParsePort(Environment.CommandLine);
            UnityEngine.Debug.Log("Is Additional. " +
                $"\nCommand Line [{Environment.CommandLine}] " +
                $"\nCurrent Scripting Defines [{PlayerSettings.GetScriptingDefineSymbols(BuildTarget)}]");
            SocketLayer.OpenListenerOnFile($"{MessageEndpoint.Playmode}{port}");
            SocketLayer.OpenListenerOnFile($"{MessageEndpoint.Scene}{port}");
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (arg == CommandLineParams.Original) {
                    UntilExitSettings.Coordinator_ParentProcessID = args[i + 1];
                }
            }
            var backgroundThread = new Thread(BackgroundUpdate);
            backgroundThread.Start();
            EditorApplication.playModeStateChanged += AdditionalCoordinatePlaymodeStateChanged;
            EditorApplication.update += AdditionalUpdate;
        }
    }

    public static void TestComplete()
    {
        UnityEngine.Debug.Log("<color=green>Tests Complete!</color>");
        if (!IsAdditional()) {
            UntilExitSettings.Coordinator_TestState = EditorStates.RunningPostTest;
            EditorApplication.isPlaying = false;
        } // The additional editors will shut off when the original sends a message to them
    }// Runs the Post Test step after leaving Playmode because of domain reload and Editor availability

    private static void BackgroundUpdate()
    {
        while (true) {
            EditorApplication.delayCall += () =>  // Ensure we're on the main thread for Unity operations
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            };
            Thread.Sleep(1000);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private static void OriginalCoordinatePlaymodeStateChanged(PlayModeStateChange playmodeState)
    {
        var playSetting = EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal;
        UnityEngine.Debug.Log($"OriginalCoordinatePlaymodeStateChanged {playmodeState} {playSetting}");
        if (playSetting == 0) return; // This is what prevents us writing out to our sockets on Playmode and TestAndPlaymode

        if (playmodeState == PlayModeStateChange.ExitingPlayMode) {
            UnityEngine.Debug.Log($"UntilExitSettings.Coordinator_TestState {UntilExitSettings.Coordinator_TestState}");
            if (UntilExitSettings.Coordinator_TestState == EditorStates.RunningPostTest) {
                UntilExitSettings.Coordinator_TestState = EditorStates.AnEditorsOpen; // Editors have to be open to be in post test
                /////////////////
                if (UntilExitSettings.Coordinator_HasTestsSetToRun) {
                    UntilExitSettings.Coordinator_HasTestsSetToRun = false;
                    UntilExitSettings.Coordinator_IsRunPostTest = true;
                }
            }
            return;
        }

        Playmode.Queue((int)playmodeState); // We queue these for later because domain reloads
    }

    private static void AdditionalCoordinatePlaymodeStateChanged(PlayModeStateChange playmodeState)
    {
        if (playmodeState != PlayModeStateChange.ExitingPlayMode) return;
        if (!UntilExitSettings.Coordinator_IsCoordinatePlayThisSessionOnAdditional) return;

        var ok = EditorUtility.DisplayDialog("Coordinated Play", "Playmode was started from the Original Editor. Please exit playmode from the Original Editor.", "OK", "Exit Playmode");
        if (ok) {
            EditorApplication.isPlaying = true;
        } else {
            UntilExitSettings.Coordinator_IsCoordinatePlayThisSessionOnAdditional = false;
        }
    }

    private static void OriginalUpdate()
    {
        if (UntilExitSettings.Coordinator_HasDelayEnterPlaymode) {
            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;
            UntilExitSettings.Coordinator_HasDelayEnterPlaymode = false;
            /////////////////
            EditorApplication.isPlaying = true; // the amount of silent failures to domain reloads is insane
        }

        if (UntilExitSettings.Coordinator_IsRunPostTest) {
            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;
            UntilExitSettings.Coordinator_IsRunPostTest = false;
            /////////////////
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var method in methods) {
                        if (method.GetCustomAttribute<AfterTestAttribute>() != null) {
                            method.Invoke(null, null);
                        }
                    }
                }
            }
        }

        if (Playmode.Count() > 0) {
            var playmode = (PlayModeStateChange)Playmode.Dequeue();
            UnityEngine.Debug.Log($"Writing command '{playmode}'");
            switch (playmode) {
                case PlayModeStateChange.EnteredPlayMode: {
                        var settings = ProjectSettings.LoadInstance();
                        var scriptingDefines = new string[CoordinatorWindow.MaximumAmountOfEditors];
                        for (var i = 0; i < CoordinatorWindow.MaximumAmountOfEditors; i++) {
                            scriptingDefines[i] = PlayerSettings.GetScriptingDefineSymbols(BuildTarget) + ";" + settings.scriptingDefineSymbols[i];
                            SocketLayer.WriteMessage($"{MessageEndpoint.Playmode}{i}", Messages.Play(scriptingDefines));
                        }
                        break;
                    }
                case PlayModeStateChange.EnteredEditMode: {
                        for (var i = 0; i < CoordinatorWindow.MaximumAmountOfEditors; i++) {
                            SocketLayer.WriteMessage($"{MessageEndpoint.Playmode}{i}", Messages.Edit);
                        }
                        break;
                    }
                case PlayModeStateChange.ExitingEditMode: break; // ignore
                case PlayModeStateChange.ExitingPlayMode: break; // ignore
                default: throw new ArgumentOutOfRangeException();// ignore
            }
        }
    }

    private static void AdditionalUpdate()
    {
        if (UntilExitSettings.Coordinator_HasDelayEnterPlaymode) {
            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;

            UntilExitSettings.Coordinator_HasDelayEnterPlaymode = false;
            EditorApplication.isPlaying = true; // the amount of silent failures to domain reloads is insane
        }

        if (sRefreshInterval > 0) {
            sRefreshInterval -= Time.deltaTime;
        } else {
            sRefreshInterval = .5f; // Refresh every half second

            var isParentProcessDead = int.TryParse(UntilExitSettings.Coordinator_ParentProcessID, out var processId) && !IsProcessAlive(processId);
            if (isParentProcessDead) {
                UnityEngine.Debug.Log($"The original '{UntilExitSettings.Coordinator_ParentProcessID}' closed so we should close ourselves");
                Process.GetCurrentProcess().Kill();
            }
        }

        foreach (var r in SocketLayer.ReceivedMessage) {
            var (endpoint, message) = (r.Key, r.Value);
            if (string.IsNullOrWhiteSpace(message)) continue;

            EndPointsToProcess.Add(endpoint);
            UnityEngine.Debug.Log($"We consumed message '{message}' on {endpoint}");
            if (endpoint == MessageEndpoint.Playmode) {
                var split = message.Split("|");
                var messageType = split[0];
                var forAdditionalOne = string.Empty;
                if (split.Length == 2) {
                    var scriptingDefinesForAllEditors = split[1];
                    var scriptingDefinesSplit = scriptingDefinesForAllEditors.Split(':');
                    forAdditionalOne = scriptingDefinesSplit[1];
                    UnityEngine.Debug.Assert(scriptingDefinesSplit.Length == CoordinatorWindow.MaximumAmountOfEditors, $"Scripting defines should always be {CoordinatorWindow.MaximumAmountOfEditors} and not {scriptingDefinesSplit.Length}");
                    UnityEngine.Debug.Log($"Updating Additional Scripting Defines '{scriptingDefinesForAllEditors}' " +
                        $"\n+ [{forAdditionalOne}]'. Then doing asset database refresh.");
                }

                if (messageType == nameof(Messages.Play)) {
                    UnityEngine.Debug.Assert(split.Length == 2);
                    UntilExitSettings.Coordinator_IsCoordinatePlayThisSessionOnAdditional = true;
                    UntilExitSettings.Coordinator_HasDelayEnterPlaymode = true;

                    foreach (var group in (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup))) {
                        if (group == BuildTargetGroup.Unknown) continue;

                        try { PlayerSettings.SetScriptingDefineSymbolsForGroup(group, forAdditionalOne); }
                        catch (ArgumentException) { }
                    }
                }
                if (messageType == nameof(Messages.Edit)) {
                    UnityEngine.Debug.Assert(split.Length == 1);
                    UntilExitSettings.Coordinator_IsCoordinatePlayThisSessionOnAdditional = false;
                    EditorApplication.isPlaying = false;
                }

                EditorApplication.delayCall += () =>
                {   // This logic is just to focus the window so we go into playmode
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView == null) sceneView = EditorWindow.CreateWindow<SceneView>();
                    sceneView.Show();
                    sceneView.Focus();
                };

                AssetDatabase.Refresh();
            }
            if (endpoint == MessageEndpoint.Scene) {
                if (SceneManager.GetActiveScene().path != message) {
                    if (Application.isPlaying) {
                        SceneManager.LoadScene(message);
                    } else {
                        EditorSceneManager.OpenScene(message);
                    }
                }
            }
        }
        foreach (var endpoint in EndPointsToProcess) {
            SocketLayer.ReceivedMessage[endpoint] = string.Empty;
        }
    }

    public static bool IsAdditional() => Environment.CommandLine.Contains(CommandLineParams.Additional);
    public static string[] GetEditorsAvailable()
    {
        var editorsAvailable = new List<string>();
        var projectName = Paths.GetProjectName();
        foreach (var dir in Directory.EnumerateDirectories(Paths.ProjectRootPath)) {
            if (dir.Contains(".git")) continue;
            if (!dir.Contains(projectName)) continue;
            editorsAvailable.Add(dir);
        }
        return editorsAvailable.ToArray();
    }

    internal static bool IsSymlinked(string destinationPath) => File.Exists(Path.Combine(destinationPath, EditorType.Symlink.ToString()));
    internal static void MarkAsSymlink(string destinationPath) => File.WriteAllText(Path.Combine(destinationPath, EditorType.Symlink.ToString()), "");
    internal static void Symlink(string sourcePath, string destinationPath)
    {
#if UNITY_EDITOR_OSX
        ExecuteBashCommandLine($"ln -s {sourcePath.Replace(" ", "\\ ")} {destinationPath.Replace(" ", "\\ ")}");
#else
        Process.Start("cmd.exe", $"/C mklink /J \"{destinationPath}\" \"{sourcePath}\"");
#endif
    }

    internal static void HardCopy(string sourcePath, string destinationPath)
    {
        var dir = new DirectoryInfo(sourcePath);
        var dirs = dir.GetDirectories(); // "get" directories before "create"

        Directory.CreateDirectory(destinationPath);

        foreach (var file in dir.GetFiles()) {
            file.CopyTo(Path.Combine(destinationPath, file.Name));
        }
        foreach (var subDir in dirs) {
            HardCopy(subDir.FullName, Path.Combine(destinationPath, subDir.Name));
        }
    }

    private static void ExecuteBashCommandLine(string command)
    {
        using (var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\"\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        }) {
            proc.Start();
            UntilExitSettings.Coordinator_ParentProcessID = proc.Id.ToString();
            proc.WaitForExit();

            if (!proc.StandardError.EndOfStream) {
                UnityEngine.Debug.LogError(proc.StandardError.ReadToEnd());
            }
        }
    }

    internal static bool IsProcessAlive(int processId)
    {
        try { return !Process.GetProcessById(processId).HasExited; }
        catch (ArgumentException) { return false; } // this should suffice unless we throw ArgumentException for multiple reasons
    }
}