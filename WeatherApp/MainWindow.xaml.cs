using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Text;
using Windows.UI;

namespace WeatherApp
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Root Grid with gradient background
            var rootGrid = new Grid();
            rootGrid.Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops = {
                    new GradientStop { Color = Color.FromArgb(0xFF, 0x6D, 0xA6, 0xF7), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0xFF, 0xB4, 0xD8, 0xFE), Offset = 1 }
                }
            };

            // Row definitions
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // App Title
            var title = new TextBlock
            {
                Text = "PlaceHolder",
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 32, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI Variable")
            };
            Grid.SetRow(title, 0);
            rootGrid.Children.Add(title);

            // Current temperature and condition
            var currentStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                Spacing = 16
            };

            // Weather icon (replace with your font if desired)
            var weatherIconsFont = new FontFamily("ms-appx:///Assets/weathericons-regular-webfont.ttf#Weather Icons");
            var currentIcon = new TextBlock
            {
                Text = "\uF00D", // Sunny
                FontFamily = weatherIconsFont,
                FontSize = 56,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xE2, 0x7D)),
                VerticalAlignment = VerticalAlignment.Center
            };
            currentStack.Children.Add(currentIcon);

            // Temperature
            var currentTemp = new TextBlock
            {
                Text = "23°",
                FontSize = 56,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center
            };
            currentStack.Children.Add(currentTemp);

            // Condition
            var currentCond = new TextBlock
            {
                Text = "Sunny",
                FontSize = 20,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            currentStack.Children.Add(currentCond);

            Grid.SetRow(currentStack, 1);
            rootGrid.Children.Add(currentStack);

            // 7-Day Forecast Cards
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 32, 0, 0),
                Content = CreateForecastPanel(weatherIconsFont)
            };
            Grid.SetRow(scrollViewer, 2);
            rootGrid.Children.Add(scrollViewer);

            // Set the content of the window
            this.Content = rootGrid;
        }

        private StackPanel CreateForecastPanel(FontFamily weatherIconsFont)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, Margin = new Thickness(24, 0, 24, 24) };

            // Data for each day
            var days = new[]
            {
        new { Day="Mon", Icon="\uF00D", Color=Color.FromArgb(0xFF,0xFF,0xE2,0x7D), High="23°", Low="16°", Desc="Sunny" },
        new { Day="Tue", Icon="\uF002", Color=Color.FromArgb(0xFF,0xA3,0xA3,0xA3), High="21°", Low="15°", Desc="Cloudy" },
        new { Day="Wed", Icon="\uF008", Color=Color.FromArgb(0xFF,0x38,0xBD,0xF8), High="19°", Low="14°", Desc="Rain" },
        new { Day="Thu", Icon="\uF002", Color=Color.FromArgb(0xFF,0xFF,0xE2,0x7D), High="22°", Low="15°", Desc="Partly Cloudy" },
        new { Day="Fri", Icon="\uF00D", Color=Color.FromArgb(0xFF,0xFF,0xE2,0x7D), High="24°", Low="17°", Desc="Sunny" },
        new { Day="Sat", Icon="\uF009", Color=Color.FromArgb(0xFF,0x38,0xBD,0xF8), High="20°", Low="13°", Desc="Showers" },
        new { Day="Sun", Icon="\uF010", Color=Color.FromArgb(0xFF,0xF8,0x71,0x71), High="18°", Low="12°", Desc="Thunderstorm" }
    };

            foreach (var d in days)
            {
                var card = new Grid
                {
                    Height = 56,
                    Margin = new Thickness(0, 0, 0, 0),
                    Background = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                    CornerRadius = new CornerRadius(16)
                };

                // Columns: Day | Icon | Desc | High | Low
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

                // Day
                var dayText = new TextBlock
                {
                    Text = d.Day,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0x22, 0x22)),
                    Margin = new Thickness(16, 0, 0, 0)
                };
                Grid.SetColumn(dayText, 0);
                card.Children.Add(dayText);

                // Icon
                var icon = new TextBlock
                {
                    Text = d.Icon,
                    FontFamily = weatherIconsFont,
                    FontSize = 28,
                    Foreground = new SolidColorBrush(d.Color),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(icon, 1);
                card.Children.Add(icon);

                // Description
                var desc = new TextBlock
                {
                    Text = d.Desc,
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0x22, 0x22, 0x22)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(desc, 2);
                card.Children.Add(desc);

                // High Temp
                var high = new TextBlock
                {
                    Text = d.High,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0x22, 0x22)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(high, 3);
                card.Children.Add(high);

                // Low Temp
                var low = new TextBlock
                {
                    Text = d.Low,
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0x22, 0x22, 0x22)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(low, 4);
                card.Children.Add(low);

                // Subtle shadow effect (optional)
                card.Shadow = new ThemeShadow();

                // Store original background for restoring
                var originalBackground = card.Background;

                // Highlight on hover
                card.PointerEntered += (s, e) =>
                {
                    card.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE0, 0xF0, 0xFF)); // Light blue highlight
                };
                card.PointerExited += (s, e) =>
                {
                    card.Background = originalBackground;
                };

                panel.Children.Add(card);
            }

            return panel;
        }

    }
}
