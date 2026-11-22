using KeyboardSwitcher.Domain;

namespace KeyboardSwitcher.Infrastructure.Settings;

internal interface IHotkeySettingsStore
{
    HotkeySettings Load();

    void Save(HotkeySettings settings);
}
