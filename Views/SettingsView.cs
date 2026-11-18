using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using pxdCommander.Core;

namespace pxdCommander.Views;

/// <summary>
/// The settings card: a list of pages on the left (Files, Look, Behaviour, Windows, About), the chosen page on the
/// right. Every change applies immediately and is saved; the card reopens on the page last looked at.
/// </summary>
public static class SettingsView
{
    public static readonly string[] Pages = ["Files", "Look", "Behaviour", "Windows", "About"];

    public static Task ShowAsync(Action applied) => Dialogs.ShowAsync<bool>(tcs =>
    {
        var s = AppSettings.Current;
        void Changed()
        {
            AppSettings.SaveSoon();
            applied();
        }

        Control Toggle(string title, string? detail, bool value, Action<bool> set)
        {
            var sw = new ToggleSwitch { IsChecked = value, OnContent = "", OffContent = "", VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };
            sw.IsCheckedChanged += (_, _) =>
            {
                set(sw.IsChecked == true);
                Changed();
            };
            return Row(title, detail, sw);
        }

        Control Choice<T>(string title, string? detail, T value, (string Label, T Value)[] options, Action<T> set) where T : notnull
        {
            var box = new ComboBox { ItemsSource = options.Select(o => o.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(options, o => o.Value.Equals(value))), MinWidth = 170 };
            box.SelectionChanged += (_, _) =>
            {
                if (box.SelectedIndex < 0) return;
                set(options[box.SelectedIndex].Value);
                Changed();
            };
            return Row(title, detail, box);
        }

        static Control Row(string title, string? detail, Control control)
        {
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = title });
            if (detail is not null) text.Children.Add(new TextBlock { Text = detail, FontSize = 11.5, Foreground = Skin.Faint, TextWrapping = TextWrapping.Wrap });
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6) };
            g.Children.Add(text);
            Grid.SetColumn(control, 1);
            control.Margin = new Thickness(16, 0, 0, 0);
            g.Children.Add(control);
            return g;
        }

        static TextBlock Section(string text) => new() { Text = text, Classes = { "label" }, Margin = new Thickness(0, 14, 0, 2) };

        static TextBlock Note(string text) => new()
        {
            Text = text,
            Foreground = Skin.Faint,
            FontSize = 11.5,
            Margin = new Thickness(0, 6),
            TextWrapping = TextWrapping.Wrap,
        };

        Control OpacityRow()
        {
            var value = new TextBlock { Text = WindowOpacity.Clamp(s.WindowOpacity) + " %", Width = 44, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            var slider = new Slider
            {
                Minimum = WindowOpacity.Min,
                Maximum = 100,
                Value = WindowOpacity.Clamp(s.WindowOpacity),
                SmallChange = 1,
                LargeChange = 10,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
            };
            slider.ValueChanged += (_, e) =>
            {
                s.WindowOpacity = WindowOpacity.Clamp((int)Math.Round(e.NewValue));
                value.Text = s.WindowOpacity + " %";
                WindowOpacity.ApplyAll();
                AppSettings.SaveSoon();
            };
            var both = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { slider, value } };
            return Row("Window opacity", "How solid the window is: lower lets the desktop show through (50–100 %). The image viewer stays solid", both);
        }

        Control DefaultFileManagerRow() => Toggle("Default file manager",
            "Folders, drives and Win+E open in pxd Commander. Explorer's own windows and its taskbar button still open Explorer. Off puts Explorer back",
            DefaultFileManager.IsSet, v =>
            {
                try
                {
                    if (v) DefaultFileManager.Set(Environment.ProcessPath!);
                    else DefaultFileManager.Unset();
                }
                catch (Exception ex)
                {
                    Toasts.Show("Couldn't change the default file manager: " + ex.Message, ToastKind.Error);
                }
            });

        var terminal = new TextBox { Text = s.TerminalCommand, PlaceholderText = "Windows Terminal, else PowerShell", Width = 240 };
        terminal.LostFocus += (_, _) =>
        {
            s.TerminalCommand = terminal.Text?.Trim() ?? "";
            Changed();
        };

        Control[] Build(string page) => page switch
        {
            "Files" =>
            [
                Section("SHOWING"),
                Toggle("Show hidden files", "Dot-files and items marked hidden (Ctrl+H)", s.ShowHidden, v => s.ShowHidden = v),
                Toggle("Show protected system files", "Hidden + system items such as desktop.ini", s.ShowSystemFiles, v => s.ShowSystemFiles = v),
                Toggle("Folders before files", null, s.FoldersFirst, v => s.FoldersFirst = v),
                Section("FOLDER SIZES"),
                Choice("Folder sizes", "Adds up folders in the background; remembered totals show at once", s.FolderSizes,
                    [("Off", FolderSizeMode.Off), ("Local drives", FolderSizeMode.LocalDrives), ("Everywhere (incl. network)", FolderSizeMode.Everywhere)], v => s.FolderSizes = v),
                Toggle("Size bars", "Proportional bars in the Size column", s.ShowSizeBars, v => s.ShowSizeBars = v),
                Section("DELETING"),
                Toggle("Ask before moving to the Recycle Bin", "Permanent deletes always ask", s.ConfirmRecycle, v => s.ConfirmRecycle = v),
            ],
            "Look" =>
            [
                Section("THEME"),
                Choice("Theme", "Dark, light, or follow Windows' own setting", s.Theme, [("Dark", "Dark"), ("Light", "Light"), ("Match Windows", "System")], v =>
                {
                    s.Theme = v;
                    ThemeManager.Apply(v);
                }),
                AccentPicker(Changed),
                OpacityRow(),
                Section("LISTS"),
                Choice("New tabs open in", null, s.DefaultView,
                    [("Details", ViewMode.Details), ("Columns", ViewMode.Columns), ("Thumbnails", ViewMode.Tiles)], v => s.DefaultView = v),
                Toggle("Compact rows", null, s.CompactRows, v => s.CompactRows = v),
                Toggle("Friendly dates", "“Today 14:32”, “Yesterday”, “Mon 09:10”", s.RelativeDates, v => s.RelativeDates = v),
                Toggle("Favorite hearts", "A heart beside each folder: click it to add or remove the folder from favorites", s.FavoriteHearts, v => s.FavoriteHearts = v),
            ],
            "Behaviour" =>
            [
                Section("OPENING"),
                Toggle("Open items with a single click", null, s.SingleClickOpen, v => s.SingleClickOpen = v),
                Toggle("Open images in the built-in viewer", "Zoom far in, step through the folder and run slideshows. Off: Windows' default app", s.OpenImagesInViewer, v => s.OpenImagesInViewer = v),
                Toggle("Reopen my tabs at start", null, s.RestoreSession, v => s.RestoreSession = v),
                Section("TERMINAL"),
                Row("Terminal command", "For “Open terminal here”. {path} is replaced by the folder", terminal),
            ],
            "Windows" => OperatingSystem.IsWindows()
                ?
                [
                    Section("WINDOWS"),
                    DefaultFileManagerRow(),
                ]
                : [Note("Nothing to set on this system.")],
            _ =>
            [
                Section("PXD COMMANDER"),
                Note($"Version {Installer.Version}"),
                Toggle("Check for updates", "Before it opens, installs a newer release when there is one", s.CheckForUpdates, v => s.CheckForUpdates = v),
                Row("Look for an update now", null, Dialogs.Btn("Check now", null, () =>
                {
                    tcs.TrySetResult(true);
                    if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life) return;
                    var windows = life.Windows.OfType<MainWindow>().ToList();
                    if ((windows.FirstOrDefault(w => w.IsActive) ?? windows.FirstOrDefault()) is { } owner)
                        _ = UpdatePrompt.CheckNowAsync(owner);
                })),
                Section("YOUR DATA"),
                Note("Settings, history, labels and remembered folder sizes are kept in " + AppPaths.Data),
            ],
        };

        // ---- page list on the left, the chosen page on the right ----
        var content = new StackPanel();
        var scroller = new ScrollViewer { Content = content, Height = 470, Padding = new Thickness(0, 0, 12, 0) };
        var list = new StackPanel { Width = 130, Margin = new Thickness(0, 4, 16, 0) };
        var tabs = new List<(string Page, Border Tab)>();

        void Show(string page)
        {
            if (!Pages.Contains(page)) page = Pages[0];
            s.SettingsPage = page;
            AppSettings.SaveSoon();
            content.Children.Clear();
            foreach (var c in Build(page)) content.Children.Add(c);
            scroller.Offset = default;
            foreach (var (p, tab) in tabs)
            {
                var on = p == page;
                tab.Background = on ? Skin.RowSelected : Avalonia.Media.Brushes.Transparent;
                ((TextBlock)tab.Child!).Foreground = on ? Skin.Text : Skin.Muted;
            }
        }

        foreach (var page in Pages)
        {
            var tab = new Border
            {
                Padding = new Thickness(10, 7),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 2),
                Background = Avalonia.Media.Brushes.Transparent,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Child = new TextBlock { Text = page },
            };
            AutomationName(tab, page);
            var p = page;
            tab.PointerPressed += (_, e) =>
            {
                Show(p);
                e.Handled = true;
            };
            tabs.Add((page, tab));
            list.Children.Add(tab);
        }

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,1,*") };
        body.Children.Add(list);
        var rule = new Border { Background = Skin.Separator, Margin = new Thickness(0, 4, 16, 4) };
        Grid.SetColumn(rule, 1);
        body.Children.Add(rule);
        Grid.SetColumn(scroller, 2);
        body.Children.Add(scroller);

        Show(s.SettingsPage);
        return new StackPanel
        {
            Children =
            {
                Dialogs.Title("Settings"),
                body,
                Dialogs.Buttons(Dialogs.Btn("Done", "primary", () => tcs.TrySetResult(true))),
            },
        };
    }, false, width: 760, focusInput: false);

    private static void AutomationName(Control c, string name) => Avalonia.Automation.AutomationProperties.SetName(c, name);

    /// <summary>
    /// Accent colour: preset swatches plus any #RRGGBB. Applies live, so the whole window (this card included)
    /// previews the colour as soon as it's picked.
    /// </summary>
    private static Control AccentPicker(Action changed)
    {
        var s = AppSettings.Current;
        var swatches = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var custom = new TextBox { Text = s.AccentColor, Width = 110, MinHeight = 30, Height = 30, PlaceholderText = "#RRGGBB", FontFamily = Skin.Mono, FontSize = 12 };
        var rings = new List<(Border Ring, string Hex)>();

        void Pick(string hex)
        {
            if (!AccentTheme.TryParse(hex, out var color)) return;
            hex = AccentTheme.ToHex(color);
            s.AccentColor = hex;
            AccentTheme.Apply(hex);
            if (custom.Text?.Trim().Equals(hex, StringComparison.OrdinalIgnoreCase) != true) custom.Text = hex;
            foreach (var (ring, h) in rings)
                ring.BorderBrush = h.Equals(hex, StringComparison.OrdinalIgnoreCase) ? Brushes.White : Brushes.Transparent;
            changed();
        }

        foreach (var (name, hex) in AccentTheme.Presets)
        {
            var dot = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(Color.Parse(hex)) };
            var ring = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(2),
                BorderBrush = hex.Equals(s.AccentColor, StringComparison.OrdinalIgnoreCase) ? Brushes.White : Brushes.Transparent,
                Child = dot,
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Background = Brushes.Transparent,
            };
            ToolTip.SetTip(ring, name);
            var value = hex;
            ring.PointerPressed += (_, e) =>
            {
                Pick(value);
                e.Handled = true;
            };
            rings.Add((ring, hex));
            swatches.Children.Add(ring);
        }
        custom.TextChanged += (_, _) =>
        {
            // Only complete colours apply, so typing doesn't flash through half-typed ones.
            var text = custom.Text?.Trim() ?? "";
            if (text.TrimStart('#').Length == 6 && AccentTheme.TryParse(text, out _)) Pick(text);
        };
        swatches.Children.Add(custom);

        var title = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Accent colour" },
                new TextBlock { Text = "Buttons, selection, highlights and the app icon", FontSize = 11.5, Foreground = Skin.Faint },
            },
        };
        return new StackPanel { Margin = new Thickness(0, 6), Children = { title, swatches } };
    }
}
