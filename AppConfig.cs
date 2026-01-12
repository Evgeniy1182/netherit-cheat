using System;
using System.IO;
using System.Text.Json;

namespace NetheritInjector
{
    public class AppConfig
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetheritInjector",
            "config.json"
        );

        public string? LastProcessName { get; set; }
        public int LastProcessId { get; set; }
        public string? LastDllPath { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public bool AutoInjectOnProcessStart { get; set; } = false;
        public int InjectionDelay { get; set; } = 0;
        public bool SaveInjectionHistory { get; set; } = true;
        public string Theme { get; set; } = "Dark";
        public bool ShowProcessInfo { get; set; } = true;

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
