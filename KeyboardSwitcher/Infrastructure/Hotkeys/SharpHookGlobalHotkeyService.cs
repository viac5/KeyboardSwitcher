using System.Diagnostics;
using KeyboardSwitcher.Domain;
using SharpHook;
using SharpHook.Native;

namespace KeyboardSwitcher.Infrastructure.Hotkeys;

internal sealed class SharpHookGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly SimpleGlobalHook _hook = new();
    private readonly LinuxEvdevKeyboardHook _linuxHook = new();
    private readonly object _stateLock = new();
    private HotkeyBinding _layoutBinding = HotkeyBinding.Parse("Alt+Shift");
    private HotkeyBinding _caseBinding = HotkeyBinding.Parse("Ctrl+Alt");
    private bool _altPressed;
    private bool _ctrlPressed;
    private bool _shiftPressed;
    private bool _modifierChordHandled;
    private bool _layoutChordHandled;
    private int _suppressionCount;
    private readonly HashSet<KeyCode> _pressedKeys = [];
    private TextAction? _pendingAction;
    private HotkeyBinding? _pendingBinding;

    public event Action<TextAction>? ActionPressed;

    public void Update(HotkeySettings settings)
    {
        lock (_stateLock)
        {
            _layoutBinding = HotkeyBinding.Parse(settings.Layout);
            _caseBinding = HotkeyBinding.Parse(settings.Case);
            _modifierChordHandled = false;
            _layoutChordHandled = false;
        }
    }

    public IDisposable Suppress() => new HookSuppression(this);

    public void Start()
    {
        if (OperatingSystem.IsLinux())
        {
            _linuxHook.KeyChanged += OnLinuxKeyChanged;
            _linuxHook.Start();
            return;
        }

        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _ = Task.Run(() => _hook.Run());
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs args)
    {
        ProcessKeyChange(args.Data.KeyCode, true);
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs args)
    {
        ProcessKeyChange(args.Data.KeyCode, false);
    }

    private void OnLinuxKeyChanged(ushort code, bool isPressed)
    {
        if (LinuxKeyCodes.TryGetValue(code, out var key))
            ProcessKeyChange(key, isPressed);
    }

    private void ProcessKeyChange(KeyCode key, bool isPressed)
    {
        TextAction? action = null;
        lock (_stateLock)
        {
            if (isPressed)
                _pressedKeys.Add(key);
            else
                _pressedKeys.Remove(key);

            UpdateModifierState();

            if (isPressed && _suppressionCount == 0 && _pendingAction is null)
            {
                if (!_layoutChordHandled && Matches(_layoutBinding, key))
                {
                    _layoutChordHandled = _layoutBinding.Key is null;
                    _pendingAction = TextAction.SwapLayout;
                    _pendingBinding = _layoutBinding;
                }
                else if (!_modifierChordHandled && Matches(_caseBinding, key))
                {
                    _modifierChordHandled = _caseBinding.Key is null;
                    _pendingAction = TextAction.ToggleCase;
                    _pendingBinding = _caseBinding;
                }
            }

            if (!isPressed && _pendingAction is { } pending && _pendingBinding is { } binding
                && !IsBindingPressed(binding))
            {
                action = pending;
                _pendingAction = null;
                _pendingBinding = null;
            }

            if (!_ctrlPressed && !_altPressed && !_shiftPressed)
            {
                _modifierChordHandled = false;
                _layoutChordHandled = false;
            }
        }

        if (action is { } triggeredAction)
        {
            Console.Error.WriteLine($"KeyboardSwitcher hotkey recognized: {triggeredAction}");
            ActionPressed?.Invoke(triggeredAction);
        }
    }

    private void UpdateModifierState()
    {
        _altPressed = _pressedKeys.Contains(KeyCode.VcLeftAlt) || _pressedKeys.Contains(KeyCode.VcRightAlt);
        _ctrlPressed = _pressedKeys.Contains(KeyCode.VcLeftControl) || _pressedKeys.Contains(KeyCode.VcRightControl);
        _shiftPressed = _pressedKeys.Contains(KeyCode.VcLeftShift) || _pressedKeys.Contains(KeyCode.VcRightShift);
    }

    private bool IsBindingPressed(HotkeyBinding binding)
    {
        return (binding.Ctrl && (_pressedKeys.Contains(KeyCode.VcLeftControl) || _pressedKeys.Contains(KeyCode.VcRightControl)))
            || (binding.Alt && (_pressedKeys.Contains(KeyCode.VcLeftAlt) || _pressedKeys.Contains(KeyCode.VcRightAlt)))
            || (binding.Shift && (_pressedKeys.Contains(KeyCode.VcLeftShift) || _pressedKeys.Contains(KeyCode.VcRightShift)))
            || (binding.Key is { } key && _pressedKeys.Contains(key));
    }

    private bool Matches(HotkeyBinding binding, KeyCode pressedKey)
    {
        var modifiersMatch = binding.Ctrl == _ctrlPressed
            && binding.Alt == _altPressed
            && binding.Shift == _shiftPressed;
        var keyMatches = binding.Key is null ? IsModifier(pressedKey) : binding.Key == pressedKey;
        return modifiersMatch && keyMatches;
    }

    private static bool IsModifier(KeyCode key) => key is
        KeyCode.VcLeftControl or KeyCode.VcRightControl or
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
        KeyCode.VcLeftShift or KeyCode.VcRightShift;

    private static readonly IReadOnlyDictionary<ushort, KeyCode> LinuxKeyCodes = CreateLinuxKeyCodes();

    private static IReadOnlyDictionary<ushort, KeyCode> CreateLinuxKeyCodes()
    {
        var mappings = new Dictionary<ushort, string>
        {
            [29] = "LeftControl", [97] = "RightControl",
            [56] = "LeftAlt", [100] = "RightAlt",
            [42] = "LeftShift", [54] = "RightShift",
            [2] = "1", [3] = "2", [4] = "3", [5] = "4", [6] = "5",
            [7] = "6", [8] = "7", [9] = "8", [10] = "9", [11] = "0",
            [16] = "Q", [17] = "W", [18] = "E", [19] = "R", [20] = "T",
            [21] = "Y", [22] = "U", [23] = "I", [24] = "O", [25] = "P",
            [30] = "A", [31] = "S", [32] = "D", [33] = "F", [34] = "G",
            [35] = "H", [36] = "J", [37] = "K", [38] = "L",
            [44] = "Z", [45] = "X", [46] = "C", [47] = "V", [48] = "B",
            [49] = "N", [50] = "M",
            [57] = "Space", [59] = "F1", [60] = "F2", [61] = "F3", [62] = "F4",
            [63] = "F5", [64] = "F6", [65] = "F7", [66] = "F8", [67] = "F9",
            [68] = "F10", [87] = "F11", [88] = "F12"
        };

        var result = new Dictionary<ushort, KeyCode>();
        foreach (var mapping in mappings)
        {
            if (Enum.TryParse<KeyCode>($"Vc{mapping.Value}", true, out var key))
                result[mapping.Key] = key;
        }

        return result;
    }

    private sealed class HookSuppression : IDisposable
    {
        private readonly SharpHookGlobalHotkeyService owner;

        public HookSuppression(SharpHookGlobalHotkeyService owner)
        {
            this.owner = owner;
            owner._suppressionCount++;
        }

        public void Dispose()
        {
            owner._suppressionCount--;
        }
    }
}
