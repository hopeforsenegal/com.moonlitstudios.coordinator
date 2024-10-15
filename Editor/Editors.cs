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
}
public enum EditorType { Symlink, HardCopy }
public static class EditorUserSettings
{
    public static EditorType Coordinator_EditorTypeOnCreate { get => (EditorType)EditorPrefs.GetInt(nameof(Coordinator_EditorTypeOnCreate), (int)EditorType.Symlink); set => EditorPrefs.SetInt(nameof(Coordinator_EditorTypeOnCreate), (int)value); }
    public static bool Coordinator_EditorCoordinatePlay { get => EditorPrefs.GetInt(nameof(Coordinator_EditorCoordinatePlay), 0) == 1; set => EditorPrefs.SetInt(nameof(Coordinator_EditorCoordinatePlay), value ? 1 : 0); }
}
/*
 * 
string defineSymbolsString = "SYMBOL1;SYMBOL2;SYMBOL3";
BuildTargetGroup targetGroup = BuildTargetGroup.Standalone;

PlayerSettings.SetScriptingDefineSymbols( NamedBuildTarget.FromBuildTargetGroup(targetGroup),  defineSymbolsString);
 */
public struct EditorInfo
{
    public string Name;
    public string ProjectPath;
    public string AssetPath;
    public string ProjectSettingsPath;

    public static EditorInfo PopulateEditorInfo(string path)
    {
        var pathByFolders = path.Split('/');
        return new EditorInfo
        {
            ProjectPath = path,
            Name = pathByFolders[pathByFolders.Length - 1],
            AssetPath = $"{path}/Assets",
            ProjectSettingsPath = $"{path}/ProjectSettings",
        };
    }
}

public static class Editors
{
    [InitializeOnLoadMethod]
    public static void OnInitialize()
    {
        if (!IsAdditional()) {
            UnityEngine.Debug.Log("Is Original");
            SocketLayer.OpenListenerOnFile("operations");
            SocketLayer.OpenSenderOnFile("keepalive");
            EditorApplication.update += AdditionalUpdate;
        } else {
            UnityEngine.Debug.Log("Is Additional");
            SocketLayer.OpenSenderOnFile("operations");
            SocketLayer.OpenListenerOnFile("keepalive");
            EditorApplication.update += OriginalUpdate;
        }
    }

    private static void OriginalUpdate()
    {
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
        var rootPath = Path.GetFullPath(Path.Combine(Paths.ProjectPath, ".."));

        return new List<string>(Directory.EnumerateDirectories(rootPath)).ToArray();
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
            proc.WaitForExit();

            if (!proc.StandardError.EndOfStream) {
                UnityEngine.Debug.LogError(proc.StandardError.ReadToEnd());
            }
        }
    }
}