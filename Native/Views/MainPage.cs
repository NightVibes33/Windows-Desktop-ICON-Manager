using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Native.Views;
public sealed class MainPage : Page
{
    readonly NavigationView nav = new() { IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed, IsSettingsVisible = false, OpenPaneLength = 218, PaneDisplayMode = NavigationViewPaneDisplayMode.Auto, ExpandedModeThresholdWidth = 1080, CompactModeThresholdWidth = 640 };
    readonly TextBlock heading = new() { FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    readonly TextBlock subtitle = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock counter = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
    readonly InfoBar feedback = new() { IsOpen = true, IsClosable = false, Padding = new Thickness(0), MinHeight = 36 };
    readonly Grid body = new();
    readonly Canvas map = new();
    readonly GridView board = new() { SelectionMode = ListViewSelectionMode.Single, IsItemClickEnabled = true, Padding = new Thickness(12), HorizontalContentAlignment = HorizontalAlignment.Stretch };
    readonly ComboBox viewChoice = new() { Width = 160 };
    readonly ComboBox category = new() { Width = 150 };
    readonly Grid visualHost = new();
    ScrollViewer? mapScroll;
    readonly ListView rows = new() { SelectionMode = ListViewSelectionMode.None, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock inspector = new() { TextWrapping = TextWrapping.Wrap, MinHeight = 24 };
    readonly Slider zoom = new() { Minimum = 75, Maximum = 200, Value = 100, Width = 180, Header = "Zoom" };
    readonly TextBox search = new() { PlaceholderText = "Search desktop", HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBox hivePath = new() { Header = "NTUSER.DAT file", PlaceholderText = "Choose a saved user registry file", MinWidth = 240 };
    readonly TextBlock hiveDetails = new() { TextWrapping = TextWrapping.Wrap };
    readonly ProgressRing busy = new() { Width = 40, Height = 40, Visibility = Visibility.Collapsed };
    readonly ComboBox theme = new() { Header = "Appearance", Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly Grid root = (Grid)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Padding='24,16,24,16' RowSpacing='12' Background='{ThemeResource ApplicationPageBackgroundThemeBrush}'/>");
    List<DesktopItem> desktop = [];
    int desktopWidth = 1920, desktopHeight = 1080;
    string route = "layout";
    bool working;
    Snapshot? inspectedHive;
    readonly List<object> evidence = [];

    public MainPage()
    {
        var brand = new StackPanel { Spacing = 8, Margin = new Thickness(20, 12, 20, 24) };
        brand.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.png")), Width = 48, Height = 48, HorizontalAlignment = HorizontalAlignment.Left });
        brand.Children.Add(new TextBlock { Text = "Desktop\nLayout Manager", FontSize = 18, MaxWidth = 150, TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        nav.PaneHeader = brand;
        viewChoice.Items.Add("Icon board"); viewChoice.Items.Add("Desktop map"); viewChoice.SelectedIndex = 0;
        foreach (var value in new[] { "All items", "Shortcuts", "Folders", "Files" }) category.Items.Add(value);
        category.SelectedIndex = 0;
        AutomationProperties.SetName(category, "Desktop item category"); AutomationProperties.SetName(viewChoice, "Layout view");
        category.SelectionChanged += (_, _) => DrawMap(); viewChoice.SelectionChanged += (_, _) => DrawMap();
        board.ItemClick += (_, args) => { if (args.ClickedItem is FrameworkElement element && element.Tag is DesktopItem item) Inspect(item); };
        nav.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, (_, _) => brand.Visibility = nav.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed);
        for (int i = 0; i < 4; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var titles = new[] { "Desktop layout", "Shortcut health", "Snapshots", "Offline recovery", "Arrangement" };
        var routes = new[] { "layout", "shortcuts", "snapshots", "hive", "repair" };
        var icons = new[] { Symbol.ViewAll, Symbol.Find, Symbol.Save, Symbol.Folder, Symbol.Setting };
        for (int i = 0; i < titles.Length; i++) nav.MenuItems.Add(new NavigationViewItem { Content = titles[i], Tag = routes[i], Icon = new SymbolIcon(icons[i]) });
        foreach (var name in new[] { "System", "Light", "Dark" }) theme.Items.Add(name);
        theme.SelectedIndex = 0;
        theme.SelectionChanged += (_, _) => RequestedTheme = theme.SelectedIndex switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
        nav.PaneFooter = theme;
        nav.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, (_, _) => theme.Visibility = nav.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed);
        var header = new StackPanel { Spacing = 4 }; header.Children.Add(heading); header.Children.Add(subtitle); root.Children.Add(header);
        var commands = new CommandBar { DefaultLabelPosition = CommandBarDefaultLabelPosition.Right, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Content = counter };
        AddCommand(commands, Symbol.Refresh, "Refresh", async () => await Refresh());
        AddCommand(commands, Symbol.Save, "Save snapshot", async () => { await Run(() => DesktopService.SaveSnapshot(), p => Notice("Snapshot saved", p)); if (route == "snapshots") await Refresh(); });
        AddCommand(commands, Symbol.Folder, "Open desktop", () => { Process.Start(new ProcessStartInfo(DesktopService.UserDesktop) { UseShellExecute = true }); return Task.CompletedTask; });
        Grid.SetRow(commands, 1); root.Children.Add(commands);
        Grid.SetRow(body, 2); root.Children.Add(body); Grid.SetRow(feedback, 3); root.Children.Add(feedback);
        nav.Content = root; Content = nav;
        nav.SelectionChanged += async (_, args) => { if (args.SelectedItem is NavigationViewItem item) { route = (string)item.Tag; await Refresh(); } };
        zoom.ValueChanged += (_, _) => DrawMap(); search.TextChanged += (_, _) => DrawMap();
        AutomationProperties.SetName(search, "Find a desktop item"); AutomationProperties.SetName(zoom, "Desktop map zoom");
        hivePath.TextChanged += (_, _) => { inspectedHive = null; hiveDetails.Text = ""; };
        Loaded += async (_, _) => { nav.SelectedItem = nav.MenuItems[0]; await WaitIdle(); if (Environment.GetCommandLineArgs().Contains("--verify")) await Verify(); };
    }
    Brush Brush(string name) => (Brush)Application.Current.Resources[name];
    static void AddCommand(CommandBar bar, Symbol icon, string label, Func<Task> action)
    {
        var button = new AppBarButton { Icon = new SymbolIcon(icon), Label = label }; ToolTipService.SetToolTip(button, label); button.Click += async (_, _) => await action(); bar.PrimaryCommands.Add(button);
    }
    static Task<T> Sta<T>(Func<T> action)
    {
        var task = new TaskCompletionSource<T>(); var thread = new Thread(() => { try { task.SetResult(action()); } catch (Exception e) { task.SetException(e); } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); return task.Task;
    }
    async Task Run<T>(Func<T> operation, Action<T> result)
    {
        if (working) return; working = true; nav.IsEnabled = false; busy.IsActive = true; busy.Visibility = Visibility.Visible;
        if (!body.Children.Contains(busy)) body.Children.Add(busy);
        try { result(await Sta(operation)); }
        catch (Exception e) { Directory.CreateDirectory(DesktopService.DataDirectory); File.AppendAllText(System.IO.Path.Combine(DesktopService.DataDirectory, "operation-errors.txt"), e.ToString() + "\n"); Notice("Unable to complete action", e.Message, InfoBarSeverity.Error); }
        finally { working = false; nav.IsEnabled = true; busy.IsActive = false; busy.Visibility = Visibility.Collapsed; }
    }
    void Notice(string title, string message = "", InfoBarSeverity severity = InfoBarSeverity.Informational) { feedback.Title = title; feedback.Message = message; feedback.Severity = severity; feedback.IsOpen = true; }
    async Task Refresh()
    {
        if (working) return;
        body.Children.Clear(); counter.Text = "";
        heading.Text = route switch { "shortcuts" => "Shortcut health", "snapshots" => "Snapshots", "hive" => "Offline recovery", "repair" => "Arrangement", _ => "Desktop layout" };
        if (route == "layout")
        {
            if (mapScroll != null) mapScroll.Content = null;
            Detach(search); Detach(zoom); Detach(map); Detach(inspector); Detach(viewChoice); Detach(category); Detach(board); Detach(visualHost);
            subtitle.Text = "Your desktop, organized and recoverable";
            var panel = new Grid { RowSpacing = 12 }; panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var tools = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
            tools.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); tools.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); tools.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            tools.RowDefinitions.Add(new() { Height = GridLength.Auto }); tools.RowDefinitions.Add(new() { Height = GridLength.Auto });
            tools.Children.Add(search); Grid.SetColumn(category, 1); tools.Children.Add(category); Grid.SetColumn(viewChoice, 2); tools.Children.Add(viewChoice); Grid.SetRow(zoom, 1); tools.Children.Add(zoom);
            panel.Children.Add(tools);
            mapScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = map };
            visualHost.Children.Clear(); visualHost.Children.Add(mapScroll); visualHost.Children.Add(board);
            var frame = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource LayerFillColorDefaultBrush}' BorderBrush='{ThemeResource CardStrokeColorDefaultBrush}' BorderThickness='1' CornerRadius='6'/>"); frame.Child = visualHost;
            Grid.SetRow(frame, 1); panel.Children.Add(frame); Grid.SetRow(inspector, 2); panel.Children.Add(inspector); body.Children.Add(panel);
            await Run(DesktopService.ReadDesktop, data => { desktop = data.Items; desktopWidth = data.Width; desktopHeight = data.Height; counter.Text = $"{desktop.Count} desktop items"; DrawMap(); Notice("Desktop up to date", $"{desktop.Count(i => i.Icon != null)} icons", InfoBarSeverity.Success); evidence.Add(new { Check = "Desktop", Count = desktop.Count, Icons = desktop.Count(i => i.Icon != null), Width = desktopWidth, Height = desktopHeight, Items = desktop.Select(i => new { i.Name, i.X, i.Y, i.Path }) }); });
        }
        else if (route == "shortcuts")
        {
            subtitle.Text = "Missing desktop entries and shortcut targets"; rows.Items.Clear(); body.Children.Add(rows);
            await Run(() => (Names: DesktopService.ParseNames(DesktopService.ActiveLayout()), Health: DesktopService.Scan()), data =>
            {
                counter.Text = $"{data.Health.Count} issues";
                if (data.Health.Count == 0) { rows.Items.Add(new TextBlock { Text = "All saved desktop entries are present. No missing shortcut targets found.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 16, 8, 16) }); Notice("Desktop shortcuts are healthy", $"Checked {data.Names.Count} saved entries", InfoBarSeverity.Success); }
                else { foreach (var item in data.Health) rows.Items.Add(HealthRow(item)); Notice("Shortcut scan complete", $"{data.Health.Count} items need attention", InfoBarSeverity.Warning); }
                evidence.Add(new { Check = "Health", SavedEntries = data.Names.Count, Issues = data.Health });
            });
        }
        else if (route == "snapshots")
        {
            subtitle.Text = "Saved layouts, including your existing backups"; rows.Items.Clear(); body.Children.Add(rows);
            await Run(DesktopService.Backups, paths => { counter.Text = $"{paths.Count} snapshots"; foreach (var path in paths) { try { var snapshot = DesktopService.LoadSnapshot(path); rows.Items.Add(SnapshotRow(path, snapshot)); } catch (Exception e) { rows.Items.Add(Label($"{System.IO.Path.GetFileName(path)}: {e.Message}")); } } Notice(paths.Count == 0 ? "No snapshots yet" : "Snapshots loaded"); });
        }
        else if (route == "hive")
        {
            Detach(hivePath); Detach(hiveDetails);
            subtitle.Text = "Inspect a saved user registry before restoring";
            var panel = new StackPanel { Spacing = 16, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(hivePath); var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            buttons.Children.Add(Button("Browse", async () => { var path = await Pick(".dat"); if (path != null) { hivePath.Text = path; inspectedHive = null; hiveDetails.Text = ""; } }));
            buttons.Children.Add(Button("Inspect", InspectSelectedHive));
            buttons.Children.Add(Button("Restore layout", async () => { if (inspectedHive == null) { Notice("Inspect the file first", "Choose a registry file and select Inspect.", InfoBarSeverity.Warning); return; } await Restore(inspectedHive); }));
            panel.Children.Add(buttons); panel.Children.Add(hiveDetails); body.Children.Add(new ScrollViewer { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top }); Notice("Offline recovery ready");
        }
        else
        {
            subtitle.Text = "Windows desktop arrangement settings"; var panel = new StackPanel { Spacing = 16 };
            bool arranged = DesktopService.AutoArrange(); panel.Children.Add(Label(arranged ? "Automatic arrangement is on." : "Automatic arrangement is off."));
            panel.Children.Add(Button("Turn off automatic arrangement", async () => { if (await Confirm("Change icon arrangement?", "A snapshot will be saved first. Explorer will restart and open folder windows may close.")) await Run(() => { DesktopService.DisableAutoArrange(); return DesktopService.AutoArrange(); }, enabled => Notice(enabled ? "Arrangement change was not retained" : "Automatic arrangement is off", "A recovery snapshot was saved.", enabled ? InfoBarSeverity.Warning : InfoBarSeverity.Success)); }));
            body.Children.Add(panel); Notice("Arrangement settings loaded");
        }
    }
    void DrawMap()
    {
        if (mapScroll == null) return;
        bool spatial = viewChoice.SelectedIndex == 1;
        board.Visibility = spatial ? Visibility.Collapsed : Visibility.Visible;
        mapScroll.Visibility = spatial ? Visibility.Visible : Visibility.Collapsed;
        zoom.Visibility = spatial ? Visibility.Visible : Visibility.Collapsed;
        var visible = desktop.Where(i => (search.Text.Length == 0 || i.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)) && MatchesCategory(i)).ToList();
        counter.Text = $"{visible.Count} items";
        if (!spatial)
        {
            board.Items.Clear();
            foreach (var item in visible.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            {
                var tile = new Grid { Width = 132, Height = 116, Padding = new Thickness(8), RowSpacing = 8, Tag = item };
                tile.RowDefinitions.Add(new() { Height = new GridLength(42) }); tile.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
                var image = ItemImage(item); image.Width = 36; image.Height = 36; tile.Children.Add(image);
                var name = new TextBlock { Text = item.Name, FontSize = 12, MaxLines = 3, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
                Grid.SetRow(name, 1); tile.Children.Add(name); ToolTipService.SetToolTip(tile, item.Name); AutomationProperties.SetName(tile, item.Name); board.Items.Add(tile);
            }
            return;
        }
        map.Children.Clear(); double scale = zoom.Value / 100;
        map.Width = desktopWidth * scale; map.Height = desktopHeight * scale;
        foreach (var item in visible)
        {
            var button = new Button { Width = Math.Max(24, 42 * scale), Height = Math.Max(24, 42 * scale), Padding = new Thickness(2), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Tag = item };
            if (item.Icon != null) { var bitmap = new BitmapImage(); using var stream = new InMemoryRandomAccessStream(); using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(item.Icon); writer.StoreAsync().AsTask().GetAwaiter().GetResult(); } stream.Seek(0); bitmap.SetSource(stream); button.Content = new Image { Source = bitmap, Stretch = Stretch.Uniform }; }
            else button.Content = new SymbolIcon(Symbol.Document);
            AutomationProperties.SetName(button, item.Name); ToolTipService.SetToolTip(button, item.Name);
            button.Click += (_, _) => Inspect(item);
            Canvas.SetLeft(button, Math.Max(0, item.X * scale)); Canvas.SetTop(button, Math.Max(0, item.Y * scale)); map.Children.Add(button);
            var name = new TextBlock { Text = item.Name, Width = 70 * scale, Height = 36, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
            Canvas.SetLeft(name, Math.Max(0, item.X * scale - 14 * scale)); Canvas.SetTop(name, item.Y * scale + 43 * scale); map.Children.Add(name);
        }
    }
    bool MatchesCategory(DesktopItem item) => category.SelectedIndex switch { 1 => item.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || item.Path.EndsWith(".url", StringComparison.OrdinalIgnoreCase), 2 => Directory.Exists(item.Path), 3 => File.Exists(item.Path) && !item.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && !item.Path.EndsWith(".url", StringComparison.OrdinalIgnoreCase), _ => true };
    async Task InspectSelectedHive()
    {
        string path = hivePath.Text.Trim();
        if (path.Length == 0) { Notice("Choose a registry file", "Select Browse and choose a saved NTUSER.DAT file.", InfoBarSeverity.Warning); return; }
        await Run(() => DesktopService.ReadHive(path), snapshot =>
        {
            if (!snapshot.Values.TryGetValue("IconLayouts", out var layouts)) throw new InvalidDataException("This registry file has desktop settings but no saved icon layout.");
            var names = DesktopService.ParseNames(Convert.FromBase64String(layouts.Data.GetString()!)); inspectedHive = snapshot;
            hiveDetails.Text = $"{names.Count} saved items  |  {snapshot.Values.Count} desktop settings\n\n" + string.Join("\n", names.Take(30).Select(n => n == "::{645FF040-5081-101B-9F08-00AA002F954E}" ? "Recycle Bin" : n));
            Notice("Offline registry inspected", System.IO.Path.GetFileName(path), InfoBarSeverity.Success);
        });
    }
    void Inspect(DesktopItem item) => inspector.Text = $"{item.Name}   |   Desktop position {item.X}, {item.Y}\n{item.Path}";
    static Image ItemImage(DesktopItem item)
    {
        var bitmap = new BitmapImage();
        if (item.Icon != null) { using var stream = new InMemoryRandomAccessStream(); using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(item.Icon); writer.StoreAsync().AsTask().GetAwaiter().GetResult(); } stream.Seek(0); bitmap.SetSource(stream); }
        return new Image { Source = bitmap, Stretch = Stretch.Uniform };
    }
    TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    static void Detach(FrameworkElement element)
    {
        if (element.Parent is Panel panel) panel.Children.Remove(element);
        else if (element.Parent is ScrollViewer scroll) scroll.Content = null;
        else if (element.Parent is Border border) border.Child = null;
    }
    Button Button(string text, Func<Task> action) { var b = new Button { Content = text }; b.Click += async (_, _) => { try { await action(); } catch (Exception e) { Notice("Unable to complete action", e.Message, InfoBarSeverity.Error); } }; return b; }
    Grid HealthRow(HealthItem item)
    {
        var grid = new Grid { ColumnSpacing = 16, Padding = new Thickness(12, 20, 12, 20) }; grid.ColumnDefinitions.Add(new() { Width = new GridLength(42) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var matched = desktop.FirstOrDefault(d => string.Equals(d.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        if (matched != null) { var image = ItemImage(matched); image.Width = 36; image.Height = 36; grid.Children.Add(image); }
        else grid.Children.Add(new SymbolIcon(Symbol.Document));
        var text = new StackPanel { Spacing = 6 }; text.Children.Add(new TextBlock { Text = System.IO.Path.GetFileNameWithoutExtension(item.Name), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }); text.Children.Add(Label(item.Status)); if (item.Source != null) text.Children.Add(Label("Available source: " + item.Source)); Grid.SetColumn(text, 1); grid.Children.Add(text);
        var fix = Button(item.Source != null ? "Restore" : "Choose source", async () => { string? source = item.Source ?? await Pick(".exe", ".lnk", ".url"); if (source == null) return; if (await Confirm("Restore this shortcut?", $"{item.Name}\n\nSource: {source}\n\nAn existing shortcut will be backed up.")) { await Run(() => { DesktopService.RestoreShortcut(item, source); return true; }, _ => Notice("Shortcut restored", item.Name, InfoBarSeverity.Success)); await Refresh(); } }); Grid.SetColumn(fix, 2); grid.Children.Add(fix); return grid;
    }
    Grid SnapshotRow(string path, Snapshot snapshot)
    {
        var grid = new Grid { ColumnSpacing = 16, Padding = new Thickness(12, 16, 12, 16) }; grid.ColumnDefinitions.Add(new() { Width = new GridLength(42) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        grid.Children.Add(new SymbolIcon(Symbol.Save));
        var text = new StackPanel { Spacing = 6 }; text.Children.Add(new TextBlock { Text = snapshot.Timestamp, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }); text.Children.Add(Label(snapshot.Note)); text.Children.Add(new TextBlock { Text = System.IO.Path.GetFileName(path), FontSize = 11, TextWrapping = TextWrapping.Wrap }); Grid.SetColumn(text, 1); grid.Children.Add(text);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(Button("Restore", async () => await Restore(snapshot)));
        var delete = new Button { Content = new SymbolIcon(Symbol.Delete), Width = 36, Height = 36, Padding = new Thickness(6) };
        ToolTipService.SetToolTip(delete, "Delete snapshot"); AutomationProperties.SetName(delete, "Delete snapshot " + snapshot.Timestamp);
        delete.Click += async (_, _) =>
        {
            if (!await Confirm("Delete this snapshot?", $"{snapshot.Timestamp}\n{snapshot.Note}\n\nThe snapshot will be moved to the Recycle Bin.")) return;
            bool deleted = false;
            await Run(() => { DesktopService.DeleteSnapshot(path); return true; }, result => deleted = result);
            if (deleted) { await Refresh(); Notice("Snapshot deleted", "Moved to the Recycle Bin.", InfoBarSeverity.Success); }
        };
        actions.Children.Add(delete); Grid.SetColumn(actions, 2); grid.Children.Add(actions); return grid;
    }
    async Task Restore(Snapshot snapshot) { if (await Confirm("Restore this desktop layout?", "The current layout will be backed up first. Explorer will restart and open folder windows may close.")) await Run(() => { DesktopService.RestoreSnapshot(snapshot); return DesktopService.ParseNames(DesktopService.ActiveLayout()).Count; }, count => Notice("Saved layout applied", $"{count} saved entries read back. Refresh the desktop map to inspect positions.", InfoBarSeverity.Success)); }
    async Task<bool> Confirm(string title, string content) => await new ContentDialog { Title = title, Content = content, PrimaryButtonText = "Continue", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, XamlRoot = XamlRoot }.ShowAsync() == ContentDialogResult.Primary;
    async Task<string?> Pick(params string[] types)
    {
        var picker = new FileOpenPicker(); foreach (var type in types) picker.FileTypeFilter.Add(type);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        return (await picker.PickSingleFileAsync())?.Path;
    }
    async Task Capture(string name)
    {
        await Task.Delay(700); var target = new RenderTargetBitmap(); await target.RenderAsync(this);
        var pixels = await target.GetPixelsAsync(); Directory.CreateDirectory(DesktopService.DataDirectory);
        using var stream = new InMemoryRandomAccessStream(); var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)target.PixelWidth, (uint)target.PixelHeight, 96, 96, pixels.ToArray()); await encoder.FlushAsync();
        using var read = stream.AsStreamForRead(); using var file = File.Create(System.IO.Path.Combine(DesktopService.DataDirectory, name + ".png")); await read.CopyToAsync(file);
    }
    async Task Verify()
    {
        try
        {
            RequestedTheme = ElementTheme.Light; await Capture("layout-light"); RequestedTheme = ElementTheme.Dark; await Capture("layout-dark");
            nav.SelectedItem = nav.MenuItems[1]; await WaitIdle(); await Capture("shortcut-health");
            nav.SelectedItem = nav.MenuItems[2]; await WaitIdle(); await Capture("snapshots");
            nav.SelectedItem = nav.MenuItems[3]; await WaitIdle();
            var arguments = Environment.GetCommandLineArgs();
            var hiveArgument = Array.IndexOf(arguments, "--verify-hive");
            if (hiveArgument >= 0 && hiveArgument + 1 < arguments.Length)
            {
                hivePath.Text = arguments[hiveArgument + 1]; await InspectSelectedHive();
                evidence.Add(new { Check = "OfflineInspect", Success = inspectedHive != null, Path = hivePath.Text, Details = hiveDetails.Text });
            }
            else evidence.Add(new { Check = "OfflineInspect", Skipped = true, Reason = "Supply --verify-hive with an offline NTUSER.DAT path to test inspection." });
            await Capture("offline-recovery");
            App.MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 640)); nav.SelectedItem = nav.MenuItems[0]; await WaitIdle(); await Capture("layout-compact");
            File.WriteAllText(System.IO.Path.Combine(DesktopService.DataDirectory, "verification.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            App.MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860)); RequestedTheme = ElementTheme.Default;
        }
        catch (Exception e) { File.WriteAllText(System.IO.Path.Combine(DesktopService.DataDirectory, "verification-error.txt"), e.ToString()); }
    }
    async Task WaitIdle() { await Task.Delay(100); while (working) await Task.Delay(100); }
}
