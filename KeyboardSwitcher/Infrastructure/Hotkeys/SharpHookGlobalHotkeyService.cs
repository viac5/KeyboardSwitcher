using KeyboardSwitcher.Domain;
using SharpHook;
using SharpHook.Native;

namespace KeyboardSwitcher.Infrastructure.Hotkeys;

internal sealed class SharpHookGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly SimpleGlobalHook _hook = new();
    private HotkeyBinding _layoutBinding = HotkeyBinding.Parse("Alt+Shift");
    private HotkeyBinding _caseBinding = HotkeyBinding.Parse("Ctrl+Alt");
    private bool _altPressed;
    private bool _ctrlPressed;
    private bool _shiftPressed;
    private bool _modifierChordHandled;
    private int _suppressionCount;

    public event Action<TextAction>? ActionPressed;

    public void Update(HotkeySettings settings)
    {
        _layoutBinding = HotkeyBinding.Parse(settings.Layout);
        _caseBinding = HotkeyBinding.Parse(settings.Case);
        _modifierChordHandled = false;
    }

    public IDisposable Suppress() => new HookSuppression(this);

    public void Start()
    {
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _ = Task.Run(() => _hook.Run());
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs args)
    {
        UpdateModifierState(args.Data.KeyCode, isPressed: true);

        if (_suppressionCount > 0)
        {
            return;
        }

        if (Matches(_layoutBinding, args.Data.KeyCode))
        {
            ActionPressed?.Invoke(TextAction.SwapLayout);
        }
        else if (!_modifierChordHandled && Matches(_caseBinding, args.Data.KeyCode))
        {
            _modifierChordHandled = _caseBinding.Key is null;
            ActionPressed?.Invoke(TextAction.ToggleCase);
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs args)
    {
        UpdateModifierState(args.Data.KeyCode, isPressed: false);
        if (!_ctrlPressed && !_altPressed && !_shiftPressed)
        {
            _modifierChordHandled = false;
        }
    }

    private void UpdateModifierState(KeyCode key, bool isPressed)
    {
        switch (key)
        {
            case KeyCode.VcLeftAlt:
            case KeyCode.VcRightAlt:
                _altPressed = isPressed;
                break;
            case KeyCode.VcLeftControl:
            case KeyCode.VcRightControl:
                _ctrlPressed = isPressed;
                break;
            case KeyCode.VcLeftShift:
            case KeyCode.VcRightShift:
                _shiftPressed = isPressed;
                break;
        }
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
