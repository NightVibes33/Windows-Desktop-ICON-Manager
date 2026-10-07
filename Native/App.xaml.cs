using Microsoft.UI.Windowing;

namespace Native
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; } = null!;
        private Window? window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            UnhandledException += (_, args) => { Directory.CreateDirectory(DesktopService.DataDirectory); File.WriteAllText(Path.Combine(DesktopService.DataDirectory, "startup-error.txt"), args.Exception.ToString()); };
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window ??= new Window();
            MainWindow = window;
            window.Title = "Desktop Layout Manager";
            window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860));
            window.ExtendsContentIntoTitleBar = true;
            window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            window.AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            window.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                //presenter.SetBorderAndTitleBar(false, false);
                presenter.PreferredMinimumWidth = 720;
                presenter.PreferredMinimumHeight = 580;
            }

            var shell = (Microsoft.UI.Xaml.Controls.Grid)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource ApplicationPageBackgroundThemeBrush}'/>");
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var titleBar = new Grid { Padding = new Thickness(18, 0, 140, 0) };
            var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new Image { Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/AppIcon.png")), Width = 22, Height = 22 });
            title.Children.Add(new TextBlock { Text = "Desktop Layout Manager", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            titleBar.Children.Add(title); shell.Children.Add(titleBar);
            var page = new MainPage(); Grid.SetRow(page, 1); shell.Children.Add(page);
            window.Content = shell; window.SetTitleBar(titleBar);
            window.Activate();
        }

    }
}
