using KeyboardSwitcher.Domain;
using SharpHook;
using SharpHook.Native;
using TextCopy;

namespace KeyboardSwitcher.Infrastructure.Clipboard;

internal sealed class ClipboardSelectedTextService : ISelectedTextService
{
    private readonly EventSimulator _simulator = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task TransformSelectionAsync(TextAction action, ITextTransformer transformer)
    {
        await _gate.WaitAsync();
        string? previousClipboard = null;
        var clipboardCaptured = false;
        var selectionMarker = $"__KeyboardSwitcherSelection_{Guid.NewGuid():N}__";

        try
        {
            previousClipboard = await ClipboardService.GetTextAsync();
            clipboardCaptured = true;
            await ClipboardService.SetTextAsync(selectionMarker);

            SimulateCopy();
            await Task.Delay(150);

            var selectedText = await ClipboardService.GetTextAsync();
            if (!string.IsNullOrEmpty(selectedText) && selectedText != selectionMarker)
            {
                await ClipboardService.SetTextAsync(transformer.Transform(selectedText, action));
                await Task.Delay(60);
                SimulatePaste();
                await Task.Delay(150);
            }
        }
        finally
        {
            if (clipboardCaptured)
            {
                await ClipboardService.SetTextAsync(previousClipboard ?? string.Empty);
            }

            _gate.Release();
        }
    }

    private void SimulateCopy()
    {
        _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
        _simulator.SimulateKeyPress(KeyCode.VcC);
        _simulator.SimulateKeyRelease(KeyCode.VcC);
        _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
    }

    private void SimulatePaste()
    {
        _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
        _simulator.SimulateKeyPress(KeyCode.VcV);
        _simulator.SimulateKeyRelease(KeyCode.VcV);
        _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
    }
}
