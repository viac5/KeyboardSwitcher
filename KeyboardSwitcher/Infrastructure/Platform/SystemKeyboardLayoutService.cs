using KeyboardSwitcher.Infrastructure.Hotkeys;
using SharpHook;
using SharpHook.Native;

namespace KeyboardSwitcher.Infrastructure.Platform;

internal sealed class SystemKeyboardLayoutService : ISystemKeyboardLayoutService
{
    private readonly EventSimulator? _simulator = OperatingSystem.IsWindows() ? new EventSimulator() : null;

    public void Toggle()
    {
        if (OperatingSystem.IsLinux())
        {
            LinuxUInputKeyboard.SendChord(125, 57); // Super+Space
            return;
        }

        if (_simulator is null)
            return;

        _simulator.SimulateKeyPress(KeyCode.VcLeftAlt);
        _simulator.SimulateKeyPress(KeyCode.VcLeftShift);
        _simulator.SimulateKeyRelease(KeyCode.VcLeftShift);
        _simulator.SimulateKeyRelease(KeyCode.VcLeftAlt);
    }
}
