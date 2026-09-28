using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MemoryInSchritten.Client.Settings;

public sealed class PlayerSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memory-InSchritten");
    private static readonly string FilePath = Path.Combine(SettingsDirectory, "settings.json");

    public string? PlayerId { get; set; }
    public string Name { get; set; } = "Player";

    public static PlayerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJson.Default.PlayerSettings) ?? new();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        return new PlayerSettings { PlayerId = ReadLegacyId() };
    }

    /// <returns>False if the file could not be written; the settings then only live for this run.</returns>
    public bool TrySave()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, SettingsJson.Default.PlayerSettings));
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Earlier versions stored only the id, in varying locations.
    private static string? ReadLegacyId()
    {
        string[] candidates = [Path.Combine(SettingsDirectory, "id.txt"), Path.Combine(AppContext.BaseDirectory, "id.txt"), "id.txt"];
        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(candidate) && File.ReadAllText(candidate).Trim() is { Length: > 0 } id) return id;
            }
            catch (IOException)
            {
            }
        }
        return null;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PlayerSettings))]
internal sealed partial class SettingsJson : JsonSerializerContext;
