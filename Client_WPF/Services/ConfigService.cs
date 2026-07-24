#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace RemoteDesktopClient.Services
{
    public static class ConfigService
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RemoteMini");
        
        private static readonly string ConfigFilePath = Path.Combine(ConfigDir, "shared_settings.json");
        private static readonly string LocalConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions { WriteIndented = true };
        private static FileSystemWatcher? _watcher;

        public static event Action<UserSettings>? OnSettingsChanged;

        static ConfigService()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                _watcher = new FileSystemWatcher(ConfigDir, "shared_settings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                DateTime lastEventTime = DateTime.MinValue;
                _watcher.Changed += (s, e) =>
                {
                    if ((DateTime.Now - lastEventTime).TotalMilliseconds < 150) return;
                    lastEventTime = DateTime.Now;

                    System.Threading.Thread.Sleep(50);
                    var updated = LoadInternal();
                    if (updated != null)
                    {
                        OnSettingsChanged?.Invoke(updated);
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigService watcher setup error: {ex.Message}");
            }
        }

        private static UserSettings? LoadInternal()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<UserSettings>(json, JsonOpts);
                }
            }
            catch { }
            return null;
        }

        public static UserSettings Load()
        {
            var settings = LoadInternal();
            if (settings != null) return settings;

            try
            {
                if (File.Exists(LocalConfigFilePath))
                {
                    string json = File.ReadAllText(LocalConfigFilePath);
                    var local = JsonSerializer.Deserialize<UserSettings>(json, JsonOpts);
                    if (local != null) return local;
                }
            }
            catch { }

            var defaultSettings = new UserSettings();
            Save(defaultSettings);
            return defaultSettings;
        }

        public static void Save(UserSettings settings)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                string json = JsonSerializer.Serialize(settings, JsonOpts);

                if (_watcher != null) _watcher.EnableRaisingEvents = false;
                File.WriteAllText(ConfigFilePath, json);
                File.WriteAllText(LocalConfigFilePath, json);
                if (_watcher != null) _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }
    }
}
