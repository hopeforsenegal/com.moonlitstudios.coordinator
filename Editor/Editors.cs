using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public enum EditorType { Symlink, HardCopy }
public static class CommandLineParams
{
    public static string Additional { get; } = "--additional";

    public static string AdditionalEditorParams { get; } = string.Join(" ", Additional);
}
public static class MessageEndpoint
{
    public static string Playmode { get; } = Path.Combine(Paths.ProjectRootPath, nameof(PlayModeStateChange));
    public static string Scene { get; } = Path.Combine(Paths.ProjectRootPath, nameof(Scene));
}
public static class Messages
{
    public const string Play = nameof(Play);
    public const string Edit = nameof(Edit);
}
public static class Paths
{
    public static string ProjectPath { get; } = Application.dataPath.Replace("/Assets", "");
    public static string ProjectRootPath { get; } = Path.GetFullPath(Path.Combine(ProjectPath, ".."));
}
public static class EditorUserSettings
{
    public static EditorType Coordinator_EditorTypeOnCreate { get => (EditorType)EditorPrefs.GetInt(nameof(Coordinator_EditorTypeOnCreate), (int)EditorType.Symlink); set => EditorPrefs.SetInt(nameof(Coordinator_EditorTypeOnCreate), (int)value); }
    public static bool Coordinator_EditorCoordinatePlay { get => EditorPrefs.GetInt(nameof(Coordinator_EditorCoordinatePlay), 0) == 1; set => EditorPrefs.SetInt(nameof(Coordinator_EditorCoordinatePlay), value ? 1 : 0); }
}
public static class UntilExitSettings
{
    // SessionState is cleared when Unity exits. But survives domain reloads.
    public static string Coordinator_ChildProcessIDs { get => SessionState.GetString(nameof(Coordinator_ChildProcessIDs), string.Empty); set => SessionState.SetString(nameof(Coordinator_ChildProcessIDs), value); }
}
/*
 * 
string defineSymbolsString = "SYMBOL1;SYMBOL2;SYMBOL3";
BuildTargetGroup targetGroup = BuildTargetGroup.Standalone;

PlayerSettings.SetScriptingDefineSymbols( NamedBuildTarget.FromBuildTargetGroup(targetGroup),  defineSymbolsString);
 */
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

public static class Editors
{
    [InitializeOnLoadMethod]
    public static void OnInitialize()
    {
        UnityEngine.Debug.Log(nameof(OnInitialize));

        if (!IsAdditional()) {
            UnityEngine.Debug.Log("Is Original");
            EditorApplication.update += OriginalUpdate;
            EditorApplication.playModeStateChanged += OriginalPlaymodeStateChanged;
            var scene = EditorSceneManager.GetActiveScene().name;
            var path = EditorSceneManager.GetActiveScene().path;
            UnityEngine.Debug.Assert(!string.IsNullOrWhiteSpace(scene));
            UnityEngine.Debug.Assert(!string.IsNullOrWhiteSpace(path));
            SocketLayer.WriteMessage(MessageEndpoint.Scene, path);
        } else {
            UnityEngine.Debug.Log("Is Additional");
            SocketLayer.OpenListenerOnFile(MessageEndpoint.Playmode);
            SocketLayer.OpenListenerOnFile(MessageEndpoint.Scene);
            EditorApplication.update += AdditionalUpdate;
        }
    }

    private static void OriginalPlaymodeStateChanged(PlayModeStateChange obj)
    {
        // Here we write into the operations file when we go into playmode so that
        // the additional goes into playmode as well
        switch (obj) {
            case PlayModeStateChange.EnteredPlayMode: SocketLayer.WriteMessage(MessageEndpoint.Playmode, Messages.Play); break;
            case PlayModeStateChange.EnteredEditMode: SocketLayer.WriteMessage(MessageEndpoint.Playmode, Messages.Edit); break;
        }
    }

    private static void OriginalUpdate()
    {
        // For the keep alive eventually
    }

    static List<string> endPointsToProcess = new List<string>();
    private static void AdditionalUpdate()
    {
        foreach (var r in SocketLayer.ReceivedMessage) {
            var endpoint = r.Key;
            var message = r.Value;
            if (!string.IsNullOrWhiteSpace(message)) {
                endPointsToProcess.Add(endpoint);
                UnityEngine.Debug.Log($"We consumed message '{message}'");
                if (endpoint == MessageEndpoint.Playmode) {
                    switch (message) {
                        case Messages.Play: EditorApplication.isPlaying = true; break;
                        case Messages.Edit: EditorApplication.isPlaying = false; break;
                        default: break;
                    }
                }
                if (endpoint == MessageEndpoint.Scene) {
                    EditorSceneManager.OpenScene(message);
                }
            }
        }
        foreach (var endpoint in endPointsToProcess) {
            SocketLayer.ReceivedMessage[endpoint] = string.Empty;
        }
    }

    public static bool IsAdditional() => System.Environment.CommandLine.Contains(CommandLineParams.Additional);
    public static string[] GetEditorsAvailable() => new List<string>(Directory.EnumerateDirectories(Paths.ProjectRootPath)).ToArray();

    public static void Symlink(string sourcePath, string destinationPath)
    {
        ExecuteBashCommandLine($"ln -s {sourcePath.Replace(" ", "\\ ")} {destinationPath.Replace(" ", "\\ ")}");
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
            UntilExitSettings.Coordinator_ChildProcessIDs = proc.Id.ToString();
            proc.WaitForExit();

            if (!proc.StandardError.EndOfStream) {
                UnityEngine.Debug.LogError(proc.StandardError.ReadToEnd());
            }
        }
    }
}