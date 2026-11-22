using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using KeyboardSwitcher.Domain;

namespace KeyboardSwitcher.Presentation;

internal sealed class MainWindow : Window
{
    private readonly ITextTransformer _transformer;

    public MainWindow( ITextTransformer transformer, HotkeySettings settings, Action<HotkeySettings> saveSettings,
        Action shutdown, bool showAtStartup )
    {
        _transformer = transformer;
        Title = "Keyboard Switcher";
        Width = 560;
        Height = 620;
        MinWidth = 480;
        MinHeight = 520;
        Background = new SolidColorBrush( Color.Parse( "#101318" ) );
        Content = BuildContent( settings, saveSettings );
        Closing += ( _, args ) =>
        {
            args.Cancel = true;
            Hide();
        };

        var trayMenu = new NativeMenu();
        var showItem = new NativeMenuItem( "Открыть настройки" );
        showItem.Click += ( _, _ ) => ShowAndActivate();
        var exitItem = new NativeMenuItem( "Выход" );
        exitItem.Click += ( _, _ ) =>
        {
            shutdown();
        };
        trayMenu.Items.Add( showItem );
        trayMenu.Items.Add( exitItem );

        using var iconStream = AssetLoader.Open(new Uri("avares://KeyboardSwitcher/Assets/tray.png"));
        var icon = new WindowIcon(new Bitmap(iconStream));
        Icon = icon;
        var tray = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "Keyboard Switcher",
            Menu = trayMenu,
            IsVisible = true
        };
        TrayIcon.SetIcons(Avalonia.Application.Current!, [tray]);

        if (showAtStartup)
        {
            Show();
            Activate();
        }
        else
        {
            ShowInTaskbar = false;
            IsVisible = false;
        }
    }

    private Control BuildContent( HotkeySettings settings, Action<HotkeySettings> saveSettings )
    {
        var layoutHotkey = new TextBox { Text = settings.Layout, Watermark = "Сочетание клавиш" };
        var caseHotkey = new TextBox { Text = settings.Case, Watermark = "Сочетание клавиш" };
        var switchSystemLayout = new CheckBox
        {
            Content = "После смены раскладки текста переключать системную раскладку",
            IsChecked = settings.SwitchSystemLayout
        };
        var testInput = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 92 };
        var result = new TextBlock { Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
        var saveButton = new Button { Content = "Сохранить сочетания", HorizontalAlignment = HorizontalAlignment.Left };
        layoutHotkey.IsReadOnly = true;
        caseHotkey.IsReadOnly = true;
        layoutHotkey.KeyDown += ( _, args ) => CaptureHotkey( layoutHotkey, args );
        caseHotkey.KeyDown += ( _, args ) => CaptureHotkey( caseHotkey, args );
        saveButton.Click += ( _, _ ) =>
        {
            settings.Layout = layoutHotkey.Text?.Trim() ?? string.Empty;
            settings.Case = caseHotkey.Text?.Trim() ?? string.Empty;
            settings.SwitchSystemLayout = switchSystemLayout.IsChecked == true;
            saveSettings( settings );
            result.Text = "Сочетания сохранены";
        };
        var resetButton = new Button
            { Content = "Вернуть настройки по умолчанию", HorizontalAlignment = HorizontalAlignment.Left };
        resetButton.Click += ( _, _ ) =>
        {
            var defaults = new HotkeySettings();
            layoutHotkey.Text = defaults.Layout;
            caseHotkey.Text = defaults.Case;
            switchSystemLayout.IsChecked = defaults.SwitchSystemLayout;
            saveSettings( defaults );
            result.Text = "Настройки по умолчанию восстановлены";
        };
        var layoutButton = new Button
            { Content = "Поменять раскладку", HorizontalAlignment = HorizontalAlignment.Left };
        layoutButton.Click += ( _, _ ) =>
            result.Text = _transformer.Transform( testInput.Text ?? string.Empty, TextAction.SwapLayout );
        var caseButton = new Button { Content = "Изменить регистр", HorizontalAlignment = HorizontalAlignment.Left };
        caseButton.Click += ( _, _ ) =>
            result.Text = _transformer.Transform( testInput.Text ?? string.Empty, TextAction.ToggleCase );

        return new Border
        {
            Padding = new Thickness( 32 ),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Keyboard Switcher", FontSize = 28, FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = "Работает в фоне и преобразует выделенный текст",
                        Foreground = new SolidColorBrush( Color.Parse( "#9BA4B5" ) )
                    },
                    new Separator(),
                    new TextBlock
                    {
                        Text = "Сочетания клавиш", FontSize = 18, FontWeight = FontWeight.SemiBold,
                        Foreground = Brushes.White
                    },
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions( "220,*" ),
                        RowDefinitions = new RowDefinitions( "Auto,Auto" ),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Раскладка", Foreground = Brushes.LightGray,
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            layoutHotkey.SetGrid( 1, 0 ),
                            new TextBlock
                            {
                                Text = "Регистр", Foreground = Brushes.LightGray,
                                VerticalAlignment = VerticalAlignment.Center
                            }.SetGrid( 0, 1 ),
                            caseHotkey.SetGrid( 1, 1 )
                        }
                    },
                    switchSystemLayout,
                    new StackPanel
                        { Orientation = Orientation.Horizontal, Spacing = 10, Children = { saveButton, resetButton } },
                    new TextBlock
                    {
                        Text = "Быстрый тест", FontSize = 18, FontWeight = FontWeight.SemiBold,
                        Foreground = Brushes.White
                    },
                    testInput,
                    new StackPanel
                        { Orientation = Orientation.Horizontal, Spacing = 10, Children = { layoutButton, caseButton } },
                    result
                }
            }
        };
    }

    private void ShowAndActivate()
    {
        ShowInTaskbar = true;
        Show();
        Activate();
    }

    private static void CaptureHotkey( TextBox target, KeyEventArgs args )
    {
        var parts = new List<string>();

        if ((args.KeyModifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((args.KeyModifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((args.KeyModifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");

        if (args.Key is Key.LeftCtrl or Key.RightCtrl && !parts.Contains("Ctrl"))
        {
            parts.Add("Ctrl");
        }
        else if (args.Key is Key.LeftAlt or Key.RightAlt && !parts.Contains("Alt"))
        {
            parts.Add("Alt");
        }
        else if (args.Key is Key.LeftShift or Key.RightShift && !parts.Contains("Shift"))
        {
            parts.Add("Shift");
        }
        else if (args.Key is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift))
        {
            parts.Add(args.Key.ToString());
        }

        if (parts.Count > 0)
        {
            target.Text = string.Join(" + ", parts);
            args.Handled = true;
        }
    }
}
