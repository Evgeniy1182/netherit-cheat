using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NetheritInjector
{
    public class InjectionHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string ProcessName { get; set; } = "";
        public int ProcessId { get; set; }
        public string DllPath { get; set; } = "";
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public static class InjectionHistory
    {
        private static readonly string HistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetheritInjector",
            "history.log"
        );

        private static readonly int MaxHistoryEntries = 100;

        public static void AddEntry(InjectionHistoryEntry entry)
        {
            try
            {
                string dir = Path.GetDirectoryName(HistoryPath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}|{entry.ProcessName}|{entry.ProcessId}|{entry.DllPath}|{entry.Success}|{entry.Message}";
                
                var lines = File.Exists(HistoryPath) 
                    ? File.ReadAllLines(HistoryPath).ToList() 
                    : new List<string>();
                
                lines.Add(line);
                
                // Ограничиваем количество записей
                if (lines.Count > MaxHistoryEntries)
                {
                    lines = lines.Skip(lines.Count - MaxHistoryEntries).ToList();
                }
                
                File.WriteAllLines(HistoryPath, lines);
            }
            catch { }
        }

        // Упрощенный метод для быстрого логирования
        public static void LogInjection(string processName, int processId, string dllPath, bool success, string message)
        {
            AddEntry(new InjectionHistoryEntry
            {
                Timestamp = DateTime.Now,
                ProcessName = processName,
                ProcessId = processId,
                DllPath = dllPath,
                Success = success,
                Message = message
            });
        }

        public static List<InjectionHistoryEntry> GetHistory()
        {
            var entries = new List<InjectionHistoryEntry>();
            
            try
            {
                if (!File.Exists(HistoryPath))
                    return entries;

                var lines = File.ReadAllLines(HistoryPath);
                
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 6)
                    {
                        entries.Add(new InjectionHistoryEntry
                        {
                            Timestamp = DateTime.Parse(parts[0]),
                            ProcessName = parts[1],
                            ProcessId = int.Parse(parts[2]),
                            DllPath = parts[3],
                            Success = bool.Parse(parts[4]),
                            Message = parts[5]
                        });
                    }
                }
            }
            catch { }
            
            return entries.OrderByDescending(e => e.Timestamp).ToList();
        }

        public static void ClearHistory()
        {
            try
            {
                if (File.Exists(HistoryPath))
                    File.Delete(HistoryPath);
            }
            catch { }
        }
    }
}
