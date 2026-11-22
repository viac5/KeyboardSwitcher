using System.Text.Json;
using KeyboardSwitcher.Domain;
using KeyboardSwitcher.Infrastructure.Settings;

namespace KeyboardSwitcher.Infrastructure.Settings;

internal sealed class JsonHotkeySettingsStore : IHotkeySettingsStore
{
    private readonly string filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KeyboardSwitcher",
        "settings.json");

    public HotkeySettings Load()
    {
        try
        {
            if (File.Exists(filePath))
            {
                return JsonSerializer.Deserialize<HotkeySettings>(File.ReadAllText(filePath))
                    ?? new HotkeySettings();
            }
        }
        catch (JsonException)
        {
            // Invalid settings should not prevent the tray application from starting.
        }
        catch (IOException)
        {
            // A temporary file-system failure falls back to default settings.
        }

        return new HotkeySettings();
    }

    public void Save(HotkeySettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
