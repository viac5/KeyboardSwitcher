using KeyboardSwitcher.Domain;

namespace KeyboardSwitcher.Infrastructure.Clipboard;

internal interface ISelectedTextService
{
    Task TransformSelectionAsync(TextAction action, ITextTransformer transformer);
}
