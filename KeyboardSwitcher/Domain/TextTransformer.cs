namespace KeyboardSwitcher.Domain;

internal sealed class TextTransformer : ITextTransformer
{
    private const string English = "`qwertyuiop[]asdfghjkl;'zxcvbnm,./~QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?";
    private const string Russian = "ёйцукенгшщзхъфывапролджэячсмитьбю.ЁЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,";

    public string Transform(string value, TextAction action) =>
        action == TextAction.SwapLayout ? SwapLayout(value) : ToggleCase(value);

    private static string SwapLayout(string value)
    {
        var result = new char[value.Length];
        for (var index = 0; index < value.Length; index++)
        {
            var englishIndex = English.IndexOf(value[index]);
            var russianIndex = Russian.IndexOf(value[index]);
            result[index] = englishIndex >= 0
                ? Russian[englishIndex]
                : russianIndex >= 0 ? English[russianIndex] : value[index];
        }

        return new string(result);
    }

    private static string ToggleCase(string value)
    {
        var hasLower = value.Any(char.IsLower);
        var hasUpper = value.Any(char.IsUpper);
        return hasUpper && !hasLower ? value.ToLowerInvariant() : value.ToUpperInvariant();
    }
}
