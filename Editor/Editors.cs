using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class Editors
{
    public static string ProjectPath { get; } = Application.dataPath.Replace("/Assets", "");
    public struct Editor
    {
        public string name;
        public string projectPath;
        public string assetPath;
        public string projectSettingsPath;

        public static Editor PopulateEditorInfo(string path)
        {
            var pathByFolders = path.Split('/');
            return new Editor
            {
                projectPath = path,
                name = pathByFolders[pathByFolders.Length - 1],
                assetPath = $"{path}/Assets",
                projectSettingsPath = $"{path}/ProjectSettings"
            };
        }
    }

    public enum Status
    {
        OK
    }

    public static string[] GetEditorsAvailable()
    {
        var rootPath = Path.Combine(ProjectPath, "..");

        return new List<string>(Directory.EnumerateDirectories(rootPath)).ToArray();
    }

    public static Status CreateSymlinkEditor(Editor original, Editor additional)
    {
        Directory.CreateDirectory(additional.projectPath);
        Symlink(original.assetPath, additional.assetPath);
        Symlink(original.projectSettingsPath, additional.projectSettingsPath);
        return Status.OK;
    }

    private static void Symlink(string sourcePath, string destinationPath)
    {
        ExecuteBashCommandLine($"ln -s {sourcePath.Replace(" ", "\\ ")} {destinationPath.Replace(" ", "\\ ")}");
    }

    public static void OpenEditorPath(string editorPath)
    {
        UnityEngine.Debug.Assert(Directory.Exists(editorPath), "No editor at location");

        Process.Start($"{EditorApplication.applicationPath}/Contents/MacOS/Unity", $"-projectPath \"{editorPath}\"");
    }

    private static void ExecuteBashCommandLine(string command)
    {
        using (var proc = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\"\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        }) {
            proc.Start();
            proc.WaitForExit();

            if (!proc.StandardError.EndOfStream) {
                UnityEngine.Debug.LogError(proc.StandardError.ReadToEnd());
            }
        }
    }
}