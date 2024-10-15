using System.IO;
using UnityEditor;
using UnityEngine;

public static class SocketLayer
{
    private static string sReceiveFilePath;
    private static bool sIsListening;
    private static float sRefreshInterval;
    private static string sSendFilePath;

    public static string ReceivedMessage;

    static SocketLayer() { EditorApplication.update += Update; }

    private static void Update()
    {
        if (sRefreshInterval > 0) {
            sRefreshInterval -= Time.deltaTime;
        } else {
            sRefreshInterval = .5f; // Refresh every half second

            if (!sIsListening) return;
            if (!File.Exists(sReceiveFilePath)) return;

            var message = File.ReadAllText(sReceiveFilePath);
            if (!string.IsNullOrEmpty(message)) {
                ReceivedMessage = message;
                File.WriteAllText(sReceiveFilePath, "");
            }
        }
    }

    public static void OpenListenerOnFile(string path)
    {
        Debug.Log($"{nameof(OpenListenerOnFile)} {path}");
        sReceiveFilePath = path;
        sIsListening = true;
    }
    public static void OpenSenderOnFile(string path)
    {
        Debug.Log($"{nameof(OpenSenderOnFile)} {path}");
        sSendFilePath = path;
    }
    public static void SendMessage(string message)
    {
        Debug.Log($"{nameof(SendMessage)} {message}");
        File.WriteAllText(sSendFilePath, message);
    }
}
