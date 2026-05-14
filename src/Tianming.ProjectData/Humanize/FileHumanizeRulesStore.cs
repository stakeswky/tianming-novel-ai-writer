using System.IO;
using System.Text.Json;

namespace TM.Services.Modules.ProjectData.Humanize
{
    public sealed class FileHumanizeRulesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly string _filePath;
        private readonly object _lock = new();

        public FileHumanizeRulesStore(string dir)
        {
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "humanize_rules.json");
        }

        public HumanizeRulesConfig Load()
        {
            lock (_lock)
            {
                if (!File.Exists(_filePath))
                {
                    return new HumanizeRulesConfig();
                }

                try
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<HumanizeRulesConfig>(json, JsonOptions) ?? new HumanizeRulesConfig();
                }
                catch (IOException)
                {
                    return new HumanizeRulesConfig();
                }
                catch (UnauthorizedAccessException)
                {
                    return new HumanizeRulesConfig();
                }
                catch (JsonException)
                {
                    return new HumanizeRulesConfig();
                }
            }
        }

        public void Save(HumanizeRulesConfig config)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(config, JsonOptions);
                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _filePath, overwrite: true);
            }
        }
    }
}
