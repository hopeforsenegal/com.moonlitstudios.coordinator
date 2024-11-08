using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum EditorType { Symlink = 1, HardCopy }
public enum CoordinationModes { Standalone, Playmode, TestAndPlaymode }
public enum TestStates { Off, Testing, PostTest }
public static class CommandLineParams
{
    public static string Additional { get; } = "--additional";
    public static string Original { get; } = "-original";
    public static string OriginalProcessID { get; } = $"{Original} {Process.GetCurrentProcess().Id}";

    public static string AdditionalEditorParams { get; } = string.Join(" ", Additional, OriginalProcessID);
}
public static class MessageEndpoint
{
    public static string Playmode { get; } = Path.Combine(Paths.ProjectRootPath, nameof(PlayModeStateChange));
    public static string Scene { get; } = Path.Combine(Paths.ProjectRootPath, nameof(UnityEngine.SceneManagement.Scene));
}
public static class Messages
{
    public const string Edit = nameof(Edit);
    internal static string Play(string[] scriptingDefineSymbols) => $"{nameof(Play)}|{string.Join(":", scriptingDefineSymbols)}";
}
public static class Paths
{
    public static string ProjectPath { get; } = Application.dataPath.Replace("/Assets", "");
    public static string ProjectRootPath { get; } = Path.GetFullPath(Path.Combine(ProjectPath, ".."));
}
public static class EditorUserSettings
{
    public static int Coordinator_CoordinatePlaySettingOnOriginal { get => EditorPrefs.GetInt(nameof(Coordinator_CoordinatePlaySettingOnOriginal), 0); set => EditorPrefs.SetInt(nameof(Coordinator_CoordinatePlaySettingOnOriginal), value); }
}
public static class UntilExitSettings // SessionState is cleared when Unity exits. But survives domain reloads.
{
    public static int Coordinator_TestState { get => SessionState.GetInt(nameof(Coordinator_TestState), 0); set => SessionState.SetInt(nameof(Coordinator_TestState), value); }
    public static string Coordinator_ParentProcessID { get => SessionState.GetString(nameof(Coordinator_ParentProcessID), string.Empty); set => SessionState.SetString(nameof(Coordinator_ParentProcessID), value); }
    public static string Coordinator_ProjectPathToChildProcessID { get => SessionState.GetString(nameof(Coordinator_ProjectPathToChildProcessID), string.Empty); set => SessionState.SetString(nameof(Coordinator_ProjectPathToChildProcessID), value); }
    public static bool Coordinator_IsCoordinatePlayThisSessionOnAdditional { get => SessionState.GetInt(nameof(Coordinator_IsCoordinatePlayThisSessionOnAdditional), 0) == 1; set => SessionState.SetInt(nameof(Coordinator_IsCoordinatePlayThisSessionOnAdditional), value ? 1 : 0); }
}
public class SessionStateConvenientListInt
{
    private readonly string m_Key;

    public SessionStateConvenientListInt(string key) => m_Key = key;
    public int[] Get() => SessionState.GetIntArray(m_Key, new int[] { });
    public void Clear() => SessionState.EraseIntArray(m_Key);
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
public struct PathToProcessId
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
public struct EditorPaths
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
    private static float sRefreshInterval;
    private static readonly List<string> EndPointsToProcess = new List<string>();
    private static readonly SessionStateConvenientListInt Playmode = new SessionStateConvenientListInt(nameof(Playmode));
    private static readonly Thread backgroundThread;
    private static NamedBuildTarget BuildTarget { get; } = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone);

    static Editors()
    {
        if (!IsAdditional()) {
            UnityEngine.Debug.Log($"Is Original. [{PlayerSettings.GetScriptingDefineSymbols(BuildTarget)}]");
            var path = EditorSceneManager.GetActiveScene().path;
            if (!string.IsNullOrWhiteSpace(path)) {
                UnityEngine.Debug.Assert(!string.IsNullOrWhiteSpace(EditorSceneManager.GetActiveScene().name));
                SocketLayer.WriteMessage(MessageEndpoint.Scene, path);
            }
            EditorApplication.playModeStateChanged += OriginalCoordinatePlaymodeStateChanged;
            EditorApplication.update += OriginalUpdate;
        } else {
            UnityEngine.Debug.Log($"Is Additional. " +
                $"\nCommand Line [{Environment.CommandLine}] " +
                $"\nCurrent Scripting Defines [{PlayerSettings.GetScriptingDefineSymbols(BuildTarget)}]");
            SocketLayer.OpenListenerOnFile(MessageEndpoint.Playmode);
            SocketLayer.OpenListenerOnFile(MessageEndpoint.Scene);
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (arg == CommandLineParams.Original) {
                    UntilExitSettings.Coordinator_ParentProcessID = args[i + 1];
                }
            }
            backgroundThread = new Thread(BackgroundUpdate);
            backgroundThread.Start();
            EditorApplication.playModeStateChanged += AdditionalCoordinatePlaymodeStateChanged;
            EditorApplication.update += AdditionalUpdate;
        }
    }
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
    }

    private static void OriginalCoordinatePlaymodeStateChanged(PlayModeStateChange playmodeState)
    {
        var playSetting = (CoordinationModes)EditorUserSettings.Coordinator_CoordinatePlaySettingOnOriginal;
        UnityEngine.Debug.Log($"OriginalCoordinatePlaymodeStateChanged {playmodeState} {playSetting}");
        if (playSetting == CoordinationModes.Standalone) return;
        if (playmodeState == PlayModeStateChange.ExitingPlayMode) return;

        if (playmodeState == PlayModeStateChange.ExitingEditMode) {
            UnityEngine.Debug.Log($"What? {playSetting}");
            if (playSetting == CoordinationModes.TestAndPlaymode) {
                UnityEngine.Debug.Log($"Updating Original Scripting Defines '{ProjectSettings.LoadInstance().globalScriptingDefineSymbols}'. Then doing asset database refresh.");
                PlayerSettings.SetScriptingDefineSymbols(BuildTarget, ProjectSettings.LoadInstance().globalScriptingDefineSymbols);
                AssetDatabase.Refresh();
            }
            return;
        }

        Playmode.Queue((int)playmodeState); // We queue these for later because domain reloads

    }

    private static void AdditionalCoordinatePlaymodeStateChanged(PlayModeStateChange playmodeState)
    {
        if (playmodeState == PlayModeStateChange.ExitingPlayMode && UntilExitSettings.Coordinator_IsCoordinatePlayThisSessionOnAdditional) {
            var ok = EditorUtility.DisplayDialog("Coordinated Play", "Playmode was started from the Original Editor. Please exit playmode from the Original Editor.", "OK", "Exit Playmode");
            if (ok) {
                EditorApplication.isPlaying = true;
            } else {
                UntilExitSettings.Coordinator_IsCoordinatePlayThisSessionOnAdditional = false;
            }
        }
    }

    private static void OriginalUpdate()
    {
        if (Playmode.Count() > 0) {
            var playmode = (PlayModeStateChange)Playmode.Dequeue();
            UnityEngine.Debug.Log($"Writing command '{playmode}'");
            switch (playmode) {
                case PlayModeStateChange.EnteredPlayMode: {
                        var settings = ProjectSettings.LoadInstance();
                        var scriptingDefines = new string[CoordinatorWindow.MaximumAmountOfEditors];
                        for (var i = 0; i < CoordinatorWindow.MaximumAmountOfEditors; i++) {
                            scriptingDefines[i] = settings.globalScriptingDefineSymbols + ";" + settings.scriptingDefineSymbols[i];
                        }
                        SocketLayer.WriteMessage(MessageEndpoint.Playmode, Messages.Play(scriptingDefines));
                    }
                    break;
                case PlayModeStateChange.EnteredEditMode: {
                        SocketLayer.WriteMessage(MessageEndpoint.Playmode, Messages.Edit);
                    }
                    break;
            }
        }
    }

    private static void AdditionalUpdate()
    {
        if (sRefreshInterval > 0) {
            sRefreshInterval -= Time.deltaTime;
        } else {
            sRefreshInterval = .5f; // Refresh every half second

            if (int.TryParse(UntilExitSettings.Coordinator_ParentProcessID, out var processId)) {
                if (!IsProcessAlive(processId)) {
                    UnityEngine.Debug.Log($"The original '{UntilExitSettings.Coordinator_ParentProcessID}' closed so we should close ourselves");
                    Process.GetCurrentProcess().Kill();
                }
            }
        }

        foreach (var r in SocketLayer.ReceivedMessage) {
            var (endpoint, message) = (r.Key, r.Value);
            if (!string.IsNullOrWhiteSpace(message)) {
                EndPointsToProcess.Add(endpoint);
                UnityEngine.Debug.Log($"We consumed message '{message}'");
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
                        EditorApplication.isPlaying = true;
                        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone), forAdditionalOne);
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
        }
        foreach (var endpoint in EndPointsToProcess) {
            SocketLayer.ReceivedMessage[endpoint] = string.Empty;
        }
    }

    public static bool IsAdditional() => Environment.CommandLine.Contains(CommandLineParams.Additional);
    public static string[] GetEditorsAvailable() => new List<string>(Directory.EnumerateDirectories(Paths.ProjectRootPath)).ToArray();

    public static bool IsSymlinked(string destinationPath) => File.Exists(Path.Combine(destinationPath, EditorType.Symlink.ToString()));
    public static void MarkAsSymlink(string destinationPath) => File.WriteAllText(Path.Combine(destinationPath, EditorType.Symlink.ToString()), "");
    public static void Symlink(string sourcePath, string destinationPath) => ExecuteBashCommandLine($"ln -s {sourcePath.Replace(" ", "\\ ")} {destinationPath.Replace(" ", "\\ ")}");

    internal static void Hardcopy(string sourcePath, string destinationPath)
    {
        var dir = new DirectoryInfo(sourcePath);
        var dirs = dir.GetDirectories(); // "get" directories before "create"

        Directory.CreateDirectory(destinationPath);

        foreach (var file in dir.GetFiles()) {
            file.CopyTo(Path.Combine(destinationPath, file.Name));
        }
        foreach (var subDir in dirs) {
            Hardcopy(subDir.FullName, Path.Combine(destinationPath, subDir.Name));
        }
    }

    public static bool IsProcessAlive(int processId)
    {
        try { return !Process.GetProcessById(processId).HasExited; }
        catch (ArgumentException) { return false; } // this should suffice unless we throw ArgumentException for multiple reasons
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
}