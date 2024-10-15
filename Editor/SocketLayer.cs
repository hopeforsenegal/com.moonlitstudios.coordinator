using System.IO;
using UnityEditor;
using UnityEngine;

public static class SocketLayer
{
    static private string receiveFilePath;
    static private bool isListening;
    private static float RefreshInterval;

    public static string receivedMessage;
    private static string sendFilePath;

    static SocketLayer() { EditorApplication.update += Update; }

    private static void Update()
    {
        if (RefreshInterval > 0) {
            RefreshInterval -= Time.deltaTime;
        } else {
            RefreshInterval = .5f; // Refresh every half second

            if (!isListening) return;
            if (!File.Exists(receiveFilePath)) return;

            string message = File.ReadAllText(receiveFilePath);
            if (!string.IsNullOrEmpty(message)) {
                receivedMessage = message;
                File.WriteAllText(receiveFilePath, "");
            }
        }
    }

    public static void OpenListenerOnFile(string path)
    {
        UnityEngine.Debug.Log($"{nameof(OpenListenerOnFile)} {path}");
        receiveFilePath = path;
        isListening = true;
    }
    public static void OpenSenderOnFile(string path)
    {
        UnityEngine.Debug.Log($"{nameof(OpenSenderOnFile)} {path}");
        sendFilePath = path;
    }
    public static void SendMessage(string message)
    {
        UnityEngine.Debug.Log($"{nameof(SendMessage)} {message}");
        File.WriteAllText(sendFilePath, message);
    }
}
