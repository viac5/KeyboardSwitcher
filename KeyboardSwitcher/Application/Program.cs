using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using KeyboardSwitcher.Domain;
using KeyboardSwitcher.Infrastructure.Clipboard;
using KeyboardSwitcher.Infrastructure.Hotkeys;
using KeyboardSwitcher.Infrastructure.Platform;
using KeyboardSwitcher.Infrastructure.Settings;
using KeyboardSwitcher.Presentation;

namespace KeyboardSwitcher.Application;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}

internal sealed class App : Avalonia.Application
{
    private readonly ITextTransformer transformer = new TextTransformer();
    private readonly ISelectedTextService selectedTextService = new ClipboardSelectedTextService(
        () => (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.Clipboard);
    private readonly IGlobalHotkeyService hotkeyService = new SharpHookGlobalHotkeyService();
    private readonly IAutostartService autostartService = new PlatformAutostartService();
    private readonly IHotkeySettingsStore settingsStore = new JsonHotkeySettingsStore();

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = settingsStore.Load();
        hotkeyService.Update(settings);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow(
                transformer,
                settings,
                SaveSettings,
                () => desktop.Shutdown(),
                showAtStartup: false);
        }

        hotkeyService.ActionPressed += action => _ = HandleActionAsync(action);
        hotkeyService.Start();
        autostartService.Enable();
        base.OnFrameworkInitializationCompleted();
    }

    private void SaveSettings(HotkeySettings settings)
    {
        settingsStore.Save(settings);
        hotkeyService.Update(settings);
    }

    private async Task HandleActionAsync(TextAction action)
    {
        using IDisposable suppression = hotkeyService.Suppress();
        try
        {
            await selectedTextService.TransformSelectionAsync(action, transformer);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"KeyboardSwitcher selection transform failed: {exception}");
        }
    }
}
