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
    private readonly IGlobalHotkeyService hotkeyService = new SharpHookGlobalHotkeyService();
    private readonly IAutostartService autostartService = new PlatformAutostartService();
    private readonly IHotkeySettingsStore settingsStore = new JsonHotkeySettingsStore();
    private readonly ISystemKeyboardLayoutService systemKeyboardLayoutService = new SystemKeyboardLayoutService();
    private HotkeySettings settings = new();
    private MainWindow? mainWindow;
    private readonly ISelectedTextService selectedTextService;

    public App()
    {
        selectedTextService = new ClipboardSelectedTextService(
            () => mainWindow?.Clipboard);
    }

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        settings = settingsStore.Load();
        hotkeyService.Update(settings);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            mainWindow = new MainWindow(
                transformer,
                settings,
                SaveSettings,
                () => desktop.Shutdown(),
                showAtStartup: false);
            desktop.MainWindow = null;
        }

        hotkeyService.ActionPressed += action => _ = HandleActionAsync(action);
        hotkeyService.Start();
        autostartService.Enable();
        base.OnFrameworkInitializationCompleted();
    }

    private void SaveSettings(HotkeySettings newSettings)
    {
        settingsStore.Save(newSettings);
        hotkeyService.Update(newSettings);
        settings = newSettings;
    }

    private async Task HandleActionAsync(TextAction action)
    {
        using IDisposable suppression = hotkeyService.Suppress();
        try
        {
            await selectedTextService.TransformSelectionAsync(action, transformer);
            if (action == TextAction.SwapLayout && settings.SwitchSystemLayout)
            {
                Console.Error.WriteLine("KeyboardSwitcher: switching system layout.");
                systemKeyboardLayoutService.Toggle();
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"KeyboardSwitcher selection transform failed: {exception}");
        }
    }
}
