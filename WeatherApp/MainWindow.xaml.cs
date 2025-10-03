using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WeatherApp
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Create the root Grid
            var rootGrid = new Grid();

            // Add resources (WeatherIcons font)
            var weatherIconsFont = new FontFamily("ms-appx:///Assets/weathericons-regular-webfont.ttf#Weather Icons");
            rootGrid.Resources.Add("WeatherIcons", weatherIconsFont);

            // Background gradient
            rootGrid.Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops = {
                    new GradientStop { Color = Color.FromArgb(0xFF, 0xF0, 0xF4, 0xFF), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0xFF, 0xD6, 0xE0, 0xF5), Offset = 1 }
                }
            };

            // Row definitions
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // App Title
            var title = new TextBlock
            {
                Text = "Weekly Weather Forecast",
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB)),
                Margin = new Thickness(0, 24, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI Variable")
            };
            Grid.SetRow(title, 0);
            rootGrid.Children.Add(title);

            // Location Placeholder
            var location = new TextBlock
            {
                Text = "Location: (Detecting...)",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x4B, 0x55, 0x63)),
                Margin = new Thickness(0, 8, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = 0.85
            };
            Grid.SetRow(location, 1);
            rootGrid.Children.Add(location);

            // Divider
            var divider = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Height = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xB6, 0xC6, 0xE3)),
                Margin = new Thickness(0, 44, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                RadiusX = 1,
                RadiusY = 1
            };
            Grid.SetRow(divider, 1);
            rootGrid.Children.Add(divider);

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
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };

            // Data for each day
            var days = new[]
            {
                new { Day="Mon", Icon="\uF00D", Color=Color.FromArgb(0xFF,0xFB,0xBF,0x24), Temp="22°C", Desc="Sunny" },
                new { Day="Tue", Icon="\uF002", Color=Color.FromArgb(0xFF,0xA3,0xA3,0xA3), Temp="19°C", Desc="Cloudy" },
                new { Day="Wed", Icon="\uF008", Color=Color.FromArgb(0xFF,0x38,0xBD,0xF8), Temp="18°C", Desc="Rain" },
                new { Day="Thu", Icon="\uF002", Color=Color.FromArgb(0xFF,0xFB,0xBF,0x24), Temp="21°C", Desc="Partly Cloudy" },
                new { Day="Fri", Icon="\uF00D", Color=Color.FromArgb(0xFF,0xFB,0xBF,0x24), Temp="23°C", Desc="Sunny" },
                new { Day="Sat", Icon="\uF009", Color=Color.FromArgb(0xFF,0x38,0xBD,0xF8), Temp="20°C", Desc="Showers" },
                new { Day="Sun", Icon="\uF010", Color=Color.FromArgb(0xFF,0xF8,0x71,0x71), Temp="17°C", Desc="Thunderstorm" }
            };

            foreach (var d in days)
            {
                var border = new Border
                {
                    Width = 120,
                    Padding = new Thickness(18),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE0, 0xE7, 0xEF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Child = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                FontFamily = weatherIconsFont,
                                Text = d.Icon,
                                FontSize = 32,
                                Foreground = new SolidColorBrush(d.Color),
                                HorizontalAlignment = HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = d.Day,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                FontSize = 17,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Foreground = new SolidColorBrush(Color.FromArgb(0xFF,0x25,0x63,0xEB))
                            },
                            new TextBlock
                            {
                                Text = d.Temp,
                                FontSize = 24,
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Foreground = new SolidColorBrush(Color.FromArgb(0xFF,0x0F,0x17,0x2A))
                            },
                            new TextBlock
                            {
                                Text = d.Desc,
                                FontStyle = Windows.UI.Text.FontStyle.Italic,
                                FontSize = 15,
                                Foreground = new SolidColorBrush(Color.FromArgb(0xFF,0x64,0x74,0x8B)),
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        }
                    }
                };
                panel.Children.Add(border);
            }

            return panel;
        }
    }
}
