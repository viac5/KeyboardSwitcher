using KeyboardSwitcher.Domain;

namespace KeyboardSwitcher.Infrastructure.Hotkeys;

internal interface IGlobalHotkeyService
{
    event Action<TextAction>? ActionPressed;

    void Update(HotkeySettings settings);

    IDisposable Suppress();

    void Start();
}
