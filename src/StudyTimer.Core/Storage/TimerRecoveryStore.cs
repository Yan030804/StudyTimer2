using System.Text.Json;
using StudyTimer.Core.Models;

namespace StudyTimer.Core.Storage;

public sealed class TimerRecoveryStore(string rootPath)
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public string FilePath { get; } = Path.Combine(rootPath, ".state.json");

    public void Save(TimerSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporaryPath = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, _options));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public TimerSnapshot? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TimerSnapshot>(File.ReadAllText(FilePath), _options);
    }

    public void Clear()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}
