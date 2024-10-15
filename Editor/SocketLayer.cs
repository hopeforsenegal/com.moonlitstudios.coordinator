using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SocketLayer
{
    private static float sRefreshInterval;

    public static Dictionary<string, string> ReceivedMessage = new Dictionary<string, string>();

    static SocketLayer() { EditorApplication.update += Update; }

    private static void Update()
    {
        if (sRefreshInterval > 0) {
            sRefreshInterval -= Time.deltaTime;
        } else {
            sRefreshInterval = .5f; // Refresh every half second

            if (ReceivedMessage.Count != 0) {
                var messageToProcess = string.Empty;
                var endPointToProcess = string.Empty;
                foreach (var path in ReceivedMessage) {
                    if (File.Exists(path.Key)) {
                        var message = File.ReadAllText(path.Key);
                        if (!string.IsNullOrEmpty(message)) {
                            messageToProcess = message;
                            endPointToProcess = path.Key;
                            break;
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(messageToProcess) && !string.IsNullOrWhiteSpace(endPointToProcess)) {
                    ReceivedMessage[endPointToProcess] = messageToProcess;
                    File.WriteAllText(endPointToProcess, string.Empty);
                }
            }
        }
    }

    public static void OpenListenerOnFile(string path)
    {
        Debug.Log($"{nameof(OpenListenerOnFile)} [{path}]");
        ReceivedMessage[path] = string.Empty;
    }

    public static void WriteMessage(string path, string message)
    {
        Debug.Log($"{nameof(WriteMessage)} [{path}] '{message}'");
        File.WriteAllText(path, message);
    }
}
