namespace KeyboardSwitcher.Domain;

internal interface ITextTransformer
{
    string Transform(string value, TextAction action);
}
