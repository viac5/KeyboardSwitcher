using SharpHook.Native;

namespace KeyboardSwitcher;

internal sealed record HotkeyBinding(bool Ctrl, bool Alt, bool Shift, KeyCode? Key)
{
    public static HotkeyBinding Parse(string value)
    {
        var ctrl = false;
        var alt = false;
        var shift = false;
        KeyCode? key = null;

        foreach (var part in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    ctrl = true;
                    break;
                case "ALT":
                    alt = true;
                    break;
                case "SHIFT":
                    shift = true;
                    break;
                default:
                    if (Enum.TryParse("Vc" + part, true, out KeyCode parsedKey))
                    {
                        key = parsedKey;
                    }
                    break;
            }
        }

        return new HotkeyBinding(ctrl, alt, shift, key);
    }
}
