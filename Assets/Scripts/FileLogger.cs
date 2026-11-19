using System;
using System.IO;
using UnityEngine;

public static class FileLogger
{
    private static StreamWriter writer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "app_log.txt");
            writer = new StreamWriter(path, false) { AutoFlush = true };
            writer.WriteLine("=== Log started " + DateTime.Now + " ===");
            writer.WriteLine("Path: " + path);
            Application.logMessageReceived += OnLog;
            Application.quitting += () =>
            {
                Application.logMessageReceived -= OnLog;
                writer?.Dispose();
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning("FileLogger could not start: " + e.Message);
        }
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (writer == null) return;
        try
        {
            writer.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [" + type + "] " + message);
            if (type == LogType.Error || type == LogType.Exception)
                writer.WriteLine(stackTrace);
        }
        catch { }
    }
}