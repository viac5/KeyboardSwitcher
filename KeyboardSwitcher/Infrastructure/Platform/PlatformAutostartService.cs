using System.Runtime.Versioning;
using Microsoft.Win32;

namespace KeyboardSwitcher.Infrastructure.Platform;

internal sealed class PlatformAutostartService : IAutostartService
{
    public void Enable()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            EnableWindows(executable);
        }
        else if (OperatingSystem.IsLinux())
        {
            EnableLinux(executable);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void EnableWindows(string executable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            "Software\\Microsoft\\Windows\\CurrentVersion\\Run");
        key?.SetValue("KeyboardSwitcher", $"\"{executable}\"");
    }

    private static void EnableLinux(string executable)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "autostart");
        Directory.CreateDirectory(directory);

        var escapedExecutable = executable.Replace("\"", "\\\"");
        var desktopEntry = "[Desktop Entry]\nType=Application\nName=Keyboard Switcher\n"
            + $"Exec=\"{escapedExecutable}\"\n"
            + "Terminal=false\nX-GNOME-Autostart-enabled=true\n";

        File.WriteAllText(Path.Combine(directory, "keyboard-switcher.desktop"), desktopEntry);
    }
}
