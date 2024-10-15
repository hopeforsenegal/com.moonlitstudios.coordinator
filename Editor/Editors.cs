using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CommandLineParams
{
    public static string Additional { get; } = "--additional";

    public static string AdditionalEditorParams { get; } = string.Join(" ", Additional);
}
public static class Paths
{
    public static string ProjectPath { get; } = Application.dataPath.Replace("/Assets", "");
    public static string ProjectRootPath { get; } = Path.GetFullPath(Path.Combine(ProjectPath, ".."));
}
public enum EditorType { Symlink, HardCopy }
public static class EditorUserSettings
{
    public static EditorType Coordinator_EditorTypeOnCreate { get => (EditorType)EditorPrefs.GetInt(nameof(Coordinator_EditorTypeOnCreate), (int)EditorType.Symlink); set => EditorPrefs.SetInt(nameof(Coordinator_EditorTypeOnCreate), (int)value); }
    public static bool Coordinator_EditorCoordinatePlay { get => EditorPrefs.GetInt(nameof(Coordinator_EditorCoordinatePlay), 0) == 1; set => EditorPrefs.SetInt(nameof(Coordinator_EditorCoordinatePlay), value ? 1 : 0); }
}
// SessionState is cleared when Unity exits. But survives domain reloads.
public static class UntilExitSettings
{
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
        UnityEngine.Debug.Log("OnInitialize");

        if (!IsAdditional()) {
            UnityEngine.Debug.Log("Is Original");
            SocketLayer.OpenSenderOnFile(Path.Combine(Paths.ProjectRootPath, "operations"));
            SocketLayer.OpenListenerOnFile(Path.Combine(Paths.ProjectRootPath, "keepalive"));
            EditorApplication.update += OriginalUpdate;
            EditorApplication.playModeStateChanged += OriginalPlaymodeStateChanged;
        } else {
            UnityEngine.Debug.Log("Is Additional");
            SocketLayer.OpenListenerOnFile(Path.Combine(Paths.ProjectRootPath, "operations"));
            SocketLayer.OpenSenderOnFile(Path.Combine(Paths.ProjectRootPath, "keepalive"));
            EditorApplication.update += AdditionalUpdate;
            SocketLayer.SendMessage("heyo");
        }
    }

    private static void OriginalPlaymodeStateChanged(PlayModeStateChange obj)
    {
        // here we right into the operations file when we go into playmode so that
        // the additional goes into playmode as well
    }

    private static void OriginalUpdate()
    {
        if (!string.IsNullOrWhiteSpace(SocketLayer.ReceivedMessage)) {
            UnityEngine.Debug.Log($"We read message {SocketLayer.ReceivedMessage}");
            SocketLayer.ReceivedMessage = string.Empty;
        }
    }

    private static void AdditionalUpdate()
    {
    }

    public static bool IsAdditional()
    {
        return System.Environment.CommandLine.Contains(CommandLineParams.Additional);
    }
    public static string[] GetEditorsAvailable()
    {
        return new List<string>(Directory.EnumerateDirectories(Paths.ProjectRootPath)).ToArray();
    }

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