using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using KeyboardSwitcher.Domain;

namespace KeyboardSwitcher.Presentation;

internal sealed class MainWindow : Window
{
    private readonly ITextTransformer _transformer;

    public MainWindow( ITextTransformer transformer, HotkeySettings settings, Action<HotkeySettings> saveSettings,
        Action shutdown )
    {
        _transformer = transformer;
        Title = "Keyboard Switcher";
        Width = 560;
        Height = 500;
        MinWidth = 480;
        MinHeight = 420;
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
        var tray = new TrayIcon { ToolTipText = "Keyboard Switcher", Menu = trayMenu };
        TrayIcon.SetIcons( Avalonia.Application.Current!, [tray] );
    }

    private Control BuildContent( HotkeySettings settings, Action<HotkeySettings> saveSettings )
    {
        var layoutHotkey = new TextBox { Text = settings.Layout, Watermark = "Сочетание клавиш" };
        var caseHotkey = new TextBox { Text = settings.Case, Watermark = "Сочетание клавиш" };
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
        Show();
        Activate();
    }

    private static void CaptureHotkey( TextBox target, KeyEventArgs args )
    {
        KeyModifiers modifiers = args.KeyModifiers;
        var parts = new List<string>();
        if ((modifiers & KeyModifiers.Control) != 0)
        {
            parts.Add( "Ctrl" );
        }

        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            parts.Add( "Alt" );
        }

        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            parts.Add( "Shift" );
        }

        if (args.Key is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
            or Key.RightShift))
        {
            parts.Add( args.Key.ToString() );
        }

        if (parts.Count > 0)
        {
            target.Text = string.Join( " + ", parts );
            args.Handled = true;
        }
    }
}
