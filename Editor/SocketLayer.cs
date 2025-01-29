using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SocketLayer
{
    private static float sRefreshInterval;

    public static readonly Dictionary<string, string> ReceivedMessage = new Dictionary<string, string>();

    static SocketLayer() { EditorApplication.update += Update; }

    private static void Update()
    {
        if (sRefreshInterval > 0) {
            sRefreshInterval -= Time.deltaTime;
        } else {
            sRefreshInterval = .5f; // Refresh every half second

            if (ReceivedMessage.Count != 0) {
                foreach (var path in ReceivedMessage) {
                    if (!File.Exists(path.Key)) continue;
                    var message = File.ReadAllText(path.Key);
                    if (string.IsNullOrEmpty(message)) continue;
                    var ourPath = path.Key.Substring(0, path.Key.Length - 1);

                    var messageToProcess = message;
                    var endPointToProcess = ourPath;
                    ReceivedMessage[endPointToProcess] = messageToProcess;
                    File.WriteAllText(path.Key, string.Empty);
                    break;
                }
            }
        }
    }

    public static void OpenListenerOnFile(string path)
    {
        var ourPath = path.Substring(0, path.Length - 1);
        Debug.Log($"{nameof(OpenListenerOnFile)} [{path}->{ourPath}]");
        ReceivedMessage[path] = string.Empty;
    }

    public static void WriteMessage(string path, string message)
    {
        Debug.Log($"{nameof(WriteMessage)} [{path}] '{message}'");
        File.WriteAllText(path, message);
    }

    public static void DeleteMessage(string path)
    {
        Debug.Log($"{nameof(DeleteMessage)} [{path}]");
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }
}