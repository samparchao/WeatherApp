using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Numerics;
using System.Threading.Tasks;
using WeatherApp.Services;
using Windows.UI;
using Windows.Storage;
using WeatherApp.Elements;

namespace WeatherApp
{
    public sealed partial class MainWindow : Window
    {
        private Grid overlayPanel;
        private Border detailsCard;

        private TextBlock detailsTitle;
        private TextBlock detailsHighLow;
        private TextBlock detailsCondValue;
        private TextBlock detailsHumidValue;
        private TextBlock detailsWindValue;
        private TextBlock detailsUVValue;
        private TextBlock detailsIcon;

        private WebView2 mapWebView;
        private Grid forecastContent;
        private Border forecastPill;
        private Border mapPill;
        private Border settingsPill;
        private bool mapInitialized;

        private Grid settingsContent;

        private TextBlock locationText;
        private double currentLatitude;
        private double currentLongitude;

        private TextBlock heroTemp;
        private TextBlock heroCondIcon;
        private TextBlock heroCondText;
        private TextBlock heroHiLo;
        private ScrollViewer forecastScroll;

        private static readonly FontFamily WeatherIconsFont =
            new("ms-appx:///Assets/weathericons-regular-webfont.ttf#Weather Icons");
        private static readonly FontFamily DisplayFont = new("Segoe UI Variable Display");
        private static readonly FontFamily TextFont = new("Segoe UI Variable Text");

        private WeatherService.WeatherData? currentWeather;
        private int? selectedDayIndex;

        private TemperatureUnit temperatureUnit = TemperatureUnit.Celsius;
        private WindSpeedUnit windSpeedUnit = WindSpeedUnit.KilometersPerHour;

        private ToggleSwitch tempUnitToggle;
        private ToggleSwitch windUnitToggle;

        private const string TemperatureUnitSettingKey = "TemperatureUnit";
        private const string WindSpeedUnitSettingKey = "WindSpeedUnit";

        private enum ActiveTab
        {
            Forecast,
            Map,
            Settings
        }

        private enum TemperatureUnit
        {
            Celsius,
            Fahrenheit
        }

        private enum WindSpeedUnit
        {
            KilometersPerHour,
            MilesPerHour
        }

        private record ForecastDay(
            int Index, string Day, string FullDay, string Icon, Color IconColor,
            string High, string Low, string Desc,
            string Humidity, string Wind, string UV);

        /// <summary>
        /// Initializes a new instance of the MainWindow class and configures the main application window.
        /// </summary>
        /// <remarks>This constructor sets up the window's title, size, and layout, and begins
        /// asynchronous initialization of location services. It is typically called by the application framework when
        /// the main window is created.</remarks>
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Weather";
            this.ExtendsContentIntoTitleBar = true;
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 780));

            LoadSettings();
            BuildUI();
            _ = InitializeLocationAsync();
        }

        private async Task InitializeLocationAsync()
        {
            try
            {
                var location = await LocationService.GetCurrentLocationAsync();
                locationText.Text = location.CityName;
                currentLatitude = location.Latitude;
                currentLongitude = location.Longitude;
            }
            catch
            {
                locationText.Text = "Location unavailable";
                return;
            }

            try
            {
                var weather = await WeatherService.GetWeatherAsync(currentLatitude, currentLongitude);
                UpdateWeatherUI(weather);
            }
            catch
            {
                heroCondText.Text = "Weather unavailable";
            }
        }

        private void UpdateWeatherUI(WeatherService.WeatherData weather)
        {
            currentWeather = weather;
            ApplyWeatherToUI(weather);
        }

        private void ApplyWeatherToUI(WeatherService.WeatherData weather)
        {
            var current = weather.Current;
            heroTemp.Text = FormatTemperature(current.Temperature);

            var (desc, icon, r, g, b) = WeatherService.MapWeatherCode(current.WeatherCode);
            heroCondIcon.Text = icon;
            heroCondIcon.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
            heroCondText.Text = desc;

            if (weather.Daily.Length > 0)
            {
                var today = weather.Daily[0];
                heroHiLo.Text = $"H:{FormatTemperature(today.TempMax)}  L:{FormatTemperature(today.TempMin)}";
            }

            var forecastDays = new ForecastDay[weather.Daily.Length];
            for (int i = 0; i < weather.Daily.Length; i++)
            {
                var d = weather.Daily[i];
                var (dayDesc, dayIcon, dr, dg, db) = WeatherService.MapWeatherCode(d.WeatherCode);
                var dayName = d.Date.Date == DateTime.Today ? "Today" : d.Date.ToString("ddd");
                var fullDay = d.Date.Date == DateTime.Today ? "Today" : d.Date.ToString("dddd");

                forecastDays[i] = new ForecastDay(
                    i, dayName, fullDay, dayIcon,
                    Color.FromArgb(0xFF, dr, dg, db),
                    FormatTemperature(d.TempMax), FormatTemperature(d.TempMin), dayDesc,
                    $"{d.Humidity:F0}%", FormatWind(d.WindSpeed, d.WindDirection), $"{d.UVIndex:F0}");
            }

            forecastScroll.Content = CreateForecastPanel(forecastDays);

            if (overlayPanel.Visibility == Visibility.Visible &&
                selectedDayIndex is int index && index >= 0 && index < forecastDays.Length)
            {
                UpdateDetails(forecastDays[index]);
            }
        }

        private void BuildUI()
        {
            Grid rootGrid = new()
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0.3, 0),
                    EndPoint = new Windows.Foundation.Point(0.7, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x0D, 0x14, 0x2B), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x1B, 0x2A, 0x4A), Offset = 0.25 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x2A, 0x40, 0x6A), Offset = 0.55 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x3D, 0x5A, 0x8C), Offset = 0.8 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x4E, 0x6E, 0xA0), Offset = 1 }
                    }
                }
            };

            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Location header
            StackPanel headerStack = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0),
                Spacing = 2
            };

            locationText = new TextBlock
            {
                Text = "Locating\u2026",
                FontSize = 34,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = DisplayFont
            };
            headerStack.Children.Add(locationText);

            headerStack.Children.Add(new TextBlock
            {
                Text = DateTime.Now.ToString("dddd, MMMM d"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = TextFont
            });

            Grid.SetRow(headerStack, 0);
            rootGrid.Children.Add(headerStack);

            // Current weather hero
            var currentStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4)
            };

            heroTemp = new TextBlock
            {
                Text = "\u2014",
                FontSize = 100,
                FontWeight = FontWeights.Thin,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = DisplayFont,
                Margin = new Thickness(0, -8, 0, -16)
            };
            currentStack.Children.Add(heroTemp);

            StackPanel condRow = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8
            };

            heroCondIcon = new TextBlock
            {
                FontFamily = WeatherIconsFont,
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xD4, 0x5E)),
                VerticalAlignment = VerticalAlignment.Center
            };
            condRow.Children.Add(heroCondIcon);

            heroCondText = new TextBlock
            {
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = TextFont
            };
            condRow.Children.Add(heroCondText);

            currentStack.Children.Add(condRow);

            heroHiLo = new TextBlock
            {
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                FontFamily = TextFont
            };
            currentStack.Children.Add(heroHiLo);

            Grid.SetRow(currentStack, 1);
            rootGrid.Children.Add(currentStack);

            // Divider
            Border divider = new()
            {
                Height = 0.5,
                Margin = new Thickness(28, 20, 28, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF))
            };
            Grid.SetRow(divider, 2);
            rootGrid.Children.Add(divider);

            // Content area with tab selector
            Grid contentArea = new();
            contentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Border tabSelector = CreateTabSelector();
            Grid.SetRow(tabSelector, 0);
            contentArea.Children.Add(tabSelector);

            // Forecast view
            forecastContent = new Grid();
            forecastContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            forecastContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var forecastHeader = new TextBlock
            {
                Text = "7-DAY FORECAST",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(32, 4, 32, 8),
                CharacterSpacing = 80,
                FontFamily = TextFont
            };
            Grid.SetRow(forecastHeader, 0);
            forecastContent.Children.Add(forecastHeader);

            forecastScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = CreateForecastPanel([])
            };
            Grid.SetRow(forecastScroll, 1);
            forecastContent.Children.Add(forecastScroll);

            Grid.SetRow(forecastContent, 1);
            contentArea.Children.Add(forecastContent);

            // Map view (initially hidden)
            mapWebView = new WebView2
            {
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(mapWebView, 1);
            contentArea.Children.Add(mapWebView);

            // Settings view (initially hidden)
            settingsContent = new Grid
            {
                Visibility = Visibility.Collapsed
            };
            settingsContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            settingsContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var settingsHeader = new TextBlock
            {
                Text = "SETTINGS",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(32, 4, 32, 8),
                CharacterSpacing = 80,
                FontFamily = TextFont
            };
            Grid.SetRow(settingsHeader, 0);
            settingsContent.Children.Add(settingsHeader);

            var settingsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = CreateSettingsPanel()
            };
            Grid.SetRow(settingsScroll, 1);
            settingsContent.Children.Add(settingsScroll);

            Grid.SetRow(settingsContent, 1);
            contentArea.Children.Add(settingsContent);

            Grid.SetRow(contentArea, 3);
            rootGrid.Children.Add(contentArea);

            // Overlay & details sheet
            BuildOverlay(rootGrid);

            this.Content = rootGrid;
        }

        private StackPanel CreateForecastPanel(ForecastDay[] forecast)
        {
            StackPanel panel = new()
            {
                Spacing = 6,
                Margin = new Thickness(20, 0, 20, 24)
            };

            foreach (var day in forecast)
            {
                var card = new Grid
                {
                    Height = 60,
                    Padding = new Thickness(16, 0, 16, 0),
                    Background = new AcrylicBrush
                    {
                        TintColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                        TintOpacity = 0.08,
                        FallbackColor = Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)
                    },
                    CornerRadius = new CornerRadius(14),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(0.5)
                };

                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
                card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });

                var dayText = new TextBlock
                {
                    Text = day.Day,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = TextFont
                };
                Grid.SetColumn(dayText, 0);
                card.Children.Add(dayText);

                var icon = new TextBlock
                {
                    Text = day.Icon,
                    FontFamily = WeatherIconsFont,
                    FontSize = 22,
                    Foreground = new SolidColorBrush(day.IconColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(icon, 1);
                card.Children.Add(icon);

                var desc = new TextBlock
                {
                    Text = day.Desc,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    FontFamily = TextFont
                };
                Grid.SetColumn(desc, 2);
                card.Children.Add(desc);

                var high = new TextBlock
                {
                    Text = day.High,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    FontFamily = TextFont
                };
                Grid.SetColumn(high, 3);
                card.Children.Add(high);

                var low = new TextBlock
                {
                    Text = day.Low,
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    FontFamily = TextFont
                };
                Grid.SetColumn(low, 4);
                card.Children.Add(low);

                AttachCardAnimations(card, day);
                panel.Children.Add(card);
            }

            return panel;
        }

        private StackPanel CreateSettingsPanel()
        {
            StackPanel panel = new()
            {
                Spacing = 12,
                Margin = new Thickness(20, 0, 20, 24)
            };

            panel.Children.Add(CreateSettingsCard(
                "About",
                new TextBlock
                {
                    Text = AppInfo.AboutText,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont
                }));

            panel.Children.Add(CreateSettingsCard(
                "Temperature",
                CreateUnitToggle("Celsius", "Fahrenheit", isOn =>
                {
                    temperatureUnit = isOn ? TemperatureUnit.Fahrenheit : TemperatureUnit.Celsius;
                    SaveSettings();
                    RefreshWeatherUI();
                }, temperatureUnit == TemperatureUnit.Fahrenheit, out tempUnitToggle)));

            panel.Children.Add(CreateSettingsCard(
                "Wind Speed",
                CreateUnitToggle("km/h", "mph", isOn =>
                {
                    windSpeedUnit = isOn ? WindSpeedUnit.MilesPerHour : WindSpeedUnit.KilometersPerHour;
                    SaveSettings();
                    RefreshWeatherUI();
                }, windSpeedUnit == WindSpeedUnit.MilesPerHour, out windUnitToggle)));

            panel.Children.Add(CreateSettingsCard(
                "Version",
                new TextBlock
                {
                    Text = AppInfo.Version,
                    FontSize = 15,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontFamily = DisplayFont
                }));

            return panel;
        }

        private Border CreateSettingsCard(string title, UIElement content)
        {
            StackPanel stack = new()
            {
                Spacing = 8
            };

            stack.Children.Add(new TextBlock
            {
                Text = title.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
                CharacterSpacing = 60,
                FontFamily = TextFont
            });

            stack.Children.Add(content);

            return new Border
            {
                Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                    TintOpacity = 0.08,
                    FallbackColor = Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)
                },
                CornerRadius = new CornerRadius(14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(16, 12, 16, 12),
                Child = stack
            };
        }

        private Grid CreateUnitToggle(string offLabel, string onLabel, Action<bool> onToggle, bool isOn, out ToggleSwitch toggle)
        {
            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = $"{offLabel} / {onLabel}",
                FontSize = 15,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = DisplayFont
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var localToggle = new ToggleSwitch
            {
                OffContent = offLabel,
                OnContent = onLabel,
                IsOn = isOn,
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 90
            };
            localToggle.Toggled += (s, e) => onToggle(localToggle.IsOn);
            Grid.SetColumn(localToggle, 1);
            grid.Children.Add(localToggle);

            toggle = localToggle;

            return grid;
        }

        private void AttachCardAnimations(Grid card, ForecastDay day)
        {
            Compositor compositor = null;
            Microsoft.UI.Composition.Visual visual = null;

            card.Loaded += (s, e) =>
            {
                visual = ElementCompositionPreview.GetElementVisual(card);
                compositor = visual.Compositor;
                visual.CenterPoint = new Vector3(
                    (float)card.ActualWidth / 2,
                    (float)card.ActualHeight / 2, 0);
            };

            card.SizeChanged += (s, e) =>
            {
                if (visual != null)
                    visual.CenterPoint = new Vector3(
                        (float)e.NewSize.Width / 2,
                        (float)e.NewSize.Height / 2, 0);
            };

            card.PointerEntered += (s, e) =>
            {
                if (compositor == null) return;

                var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(1f, new Vector3(1.02f, 1.02f, 1f),
                    compositor.CreateCubicBezierEasingFunction(
                        new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
                scaleAnim.Duration = TimeSpan.FromMilliseconds(200);
                visual.StartAnimation("Scale", scaleAnim);

                card.Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                    TintOpacity = 0.14,
                    FallbackColor = Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)
                };
            };

            card.PointerExited += (s, e) =>
            {
                if (compositor == null) return;

                var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(1f, Vector3.One,
                    compositor.CreateCubicBezierEasingFunction(
                        new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
                scaleAnim.Duration = TimeSpan.FromMilliseconds(250);
                visual.StartAnimation("Scale", scaleAnim);

                card.Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                    TintOpacity = 0.08,
                    FallbackColor = Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)
                };
            };

            card.PointerPressed += (s, e) =>
            {
                if (compositor == null) return;
                var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(1f, new Vector3(0.97f, 0.97f, 1f));
                scaleAnim.Duration = TimeSpan.FromMilliseconds(80);
                visual.StartAnimation("Scale", scaleAnim);
            };

            card.PointerReleased += (s, e) =>
            {
                if (compositor == null) return;
                var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(1f, new Vector3(1.02f, 1.02f, 1f),
                    compositor.CreateCubicBezierEasingFunction(
                        new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
                scaleAnim.Duration = TimeSpan.FromMilliseconds(150);
                visual.StartAnimation("Scale", scaleAnim);
            };

            card.Tapped += (s, e) =>
            {
                ShowDetails(day);
                e.Handled = true;
            };
        }

        private Border CreateTabSelector()
        {
            Border outerBorder = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4),
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(4)
            };

            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            forecastPill = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(18, 7, 18, 7),
                Child = new TextBlock
                {
                    Text = "Forecast",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontFamily = TextFont
                }
            };
            forecastPill.Tapped += (s, e) => { SwitchToForecast(); e.Handled = true; };
            Grid.SetColumn(forecastPill, 0);
            grid.Children.Add(forecastPill);

            mapPill = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(18, 7, 18, 7),
                Child = new TextBlock
                {
                    Text = "Temperature Map",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont
                }
            };
            mapPill.Tapped += (s, e) => { SwitchToMap(); e.Handled = true; };
            Grid.SetColumn(mapPill, 1);
            grid.Children.Add(mapPill);

            settingsPill = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(18, 7, 18, 7),
                Child = new TextBlock
                {
                    Text = "Settings",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont
                }
            };
            settingsPill.Tapped += (s, e) => { SwitchToSettings(); e.Handled = true; };
            Grid.SetColumn(settingsPill, 2);
            grid.Children.Add(settingsPill);

            outerBorder.Child = grid;
            return outerBorder;
        }

        private void UpdateTabAppearance(ActiveTab activeTab)
        {
            UpdateTabPill(forecastPill, activeTab == ActiveTab.Forecast);
            UpdateTabPill(mapPill, activeTab == ActiveTab.Map);
            UpdateTabPill(settingsPill, activeTab == ActiveTab.Settings);
        }

        private static void UpdateTabPill(Border pill, bool isActive)
        {
            pill.Background = new SolidColorBrush(
                isActive ? Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
            ((TextBlock)pill.Child).Foreground = new SolidColorBrush(
                isActive ? Colors.White : Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
        }

        private void SwitchToForecast()
        {
            if (forecastContent.Visibility == Visibility.Visible) return;

            mapWebView.Visibility = Visibility.Collapsed;
            settingsContent.Visibility = Visibility.Collapsed;
            forecastContent.Visibility = Visibility.Visible;
            UpdateTabAppearance(ActiveTab.Forecast);

            Visual visual = ElementCompositionPreview.GetElementVisual(forecastContent);
            Compositor compositor = visual.Compositor;

            ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
            fade.Duration = TimeSpan.FromMilliseconds(300);
            visual.StartAnimation("Opacity", fade);
        }

        private async void SwitchToMap()
        {
            if (mapWebView.Visibility == Visibility.Visible) return;

            forecastContent.Visibility = Visibility.Collapsed;
            settingsContent.Visibility = Visibility.Collapsed;
            mapWebView.Visibility = Visibility.Visible;
            UpdateTabAppearance(ActiveTab.Map);

            if (!mapInitialized)
            {
                mapInitialized = true;
                await mapWebView.EnsureCoreWebView2Async();
                mapWebView.DefaultBackgroundColor = Color.FromArgb(0xFF, 0x0D, 0x14, 0x2B);
                mapWebView.NavigateToString(WindowElements.GetMapHtml());
            }

            Visual visual = ElementCompositionPreview.GetElementVisual(mapWebView);
            Compositor compositor = visual.Compositor;

            ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
            fade.Duration = TimeSpan.FromMilliseconds(300);
            visual.StartAnimation("Opacity", fade);
        }

        private void SwitchToSettings()
        {
            if (settingsContent.Visibility == Visibility.Visible) return;

            forecastContent.Visibility = Visibility.Collapsed;
            mapWebView.Visibility = Visibility.Collapsed;
            settingsContent.Visibility = Visibility.Visible;
            UpdateTabAppearance(ActiveTab.Settings);

            Visual visual = ElementCompositionPreview.GetElementVisual(settingsContent);
            Compositor compositor = visual.Compositor;

            ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
            fade.Duration = TimeSpan.FromMilliseconds(300);
            visual.StartAnimation("Opacity", fade);
        }

        private void BuildOverlay(Grid rootGrid)
        {
            overlayPanel = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = 0
            };

            overlayPanel.Tapped += (s, e) =>
            {
                if (e.OriginalSource == overlayPanel)
                    HideDetails();
            };

            var cardContent = new StackPanel
            {
                Spacing = 14,
                Margin = new Thickness(28, 12, 28, 28)
            };

            // Drag handle
            cardContent.Children.Add(new Grid
            {
                Height = 20,
                Children =
                {
                    new Border
                    {
                        Height = 4,
                        Width = 40,
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            });

            // Title row with icon
            var titleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12
            };

            detailsIcon = new TextBlock
            {
                FontFamily = WeatherIconsFont,
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(detailsIcon);

            detailsTitle = new TextBlock
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = DisplayFont
            };
            titleRow.Children.Add(detailsTitle);
            cardContent.Children.Add(titleRow);

            // High / Low
            detailsHighLow = new TextBlock
            {
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                FontFamily = TextFont
            };
            cardContent.Children.Add(detailsHighLow);

            // Separator
            cardContent.Children.Add(new Border
            {
                Height = 0.5,
                Background = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 2, 0, 2)
            });

            // 2x2 detail grid
            Grid detailsGrid = new();
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel condItem = CreateDetailItem("Condition", out detailsCondValue);
            Grid.SetRow(condItem, 0); Grid.SetColumn(condItem, 0);
            detailsGrid.Children.Add(condItem);

            StackPanel humidItem = CreateDetailItem("Humidity", out detailsHumidValue);
            Grid.SetRow(humidItem, 0); Grid.SetColumn(humidItem, 1);
            detailsGrid.Children.Add(humidItem);

            StackPanel windItem = CreateDetailItem("Wind", out detailsWindValue);
            Grid.SetRow(windItem, 1); Grid.SetColumn(windItem, 0);
            detailsGrid.Children.Add(windItem);

            StackPanel uvItem = CreateDetailItem("UV Index", out detailsUVValue);
            Grid.SetRow(uvItem, 1); Grid.SetColumn(uvItem, 1);
            detailsGrid.Children.Add(uvItem);

            cardContent.Children.Add(detailsGrid);

            // Dismiss button
            Button closeBtn = new()
            {
                Content = "Dismiss",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 40,
                Margin = new Thickness(0, 8, 0, 0),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.SemiBold,
                FontFamily = TextFont,
                BorderThickness = new Thickness(0)
            };
            closeBtn.Click += (s, e) => HideDetails();
            cardContent.Children.Add(closeBtn);

            detailsCard = new Border
            {
                CornerRadius = new CornerRadius(20, 20, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MaxWidth = 460,
                MinHeight = 360,
                RenderTransform = new TranslateTransform { Y = 500 },
                Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0x14, 0x20, 0x3A),
                    TintOpacity = 0.85,
                    FallbackColor = Color.FromArgb(0xF0, 0x1A, 0x2A, 0x48)
                },
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.5, 0.5, 0.5, 0),
                Child = cardContent
            };

            overlayPanel.Children.Add(detailsCard);
            rootGrid.Children.Add(overlayPanel);
            Grid.SetRowSpan(overlayPanel, 4);
            Canvas.SetZIndex(overlayPanel, 99);
        }

        private static StackPanel CreateDetailItem(string label, out TextBlock valueBlock)
        {
            StackPanel stack = new()
            {
                Spacing = 2,
                Margin = new Thickness(0, 8, 0, 8)
            };

            stack.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
                CharacterSpacing = 60,
                FontFamily = TextFont
            });

            valueBlock = new TextBlock
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = DisplayFont
            };
            stack.Children.Add(valueBlock);

            return stack;
        }

        private void RefreshWeatherUI()
        {
            if (currentWeather == null)
            {
                return;
            }

            ApplyWeatherToUI(currentWeather);
        }

        private void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;

            if (settings.TryGetValue(TemperatureUnitSettingKey, out var tempValue) &&
                tempValue is string tempString &&
                Enum.TryParse(tempString, out TemperatureUnit storedTempUnit))
            {
                temperatureUnit = storedTempUnit;
            }

            if (settings.TryGetValue(WindSpeedUnitSettingKey, out var windValue) &&
                windValue is string windString &&
                Enum.TryParse(windString, out WindSpeedUnit storedWindUnit))
            {
                windSpeedUnit = storedWindUnit;
            }
        }

        private void SaveSettings()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings[TemperatureUnitSettingKey] = temperatureUnit.ToString();
            settings[WindSpeedUnitSettingKey] = windSpeedUnit.ToString();
        }


        private string FormatWind(double speed, double direction)
        {
            var compass = GetWindDirection(direction);
            var converted = windSpeedUnit == WindSpeedUnit.MilesPerHour
                ? speed * 0.621371
                : speed;
            var unitLabel = windSpeedUnit == WindSpeedUnit.MilesPerHour ? "mph" : "km/h";

            return string.IsNullOrWhiteSpace(compass)
                ? $"{converted:F0} {unitLabel}"
                : $"{converted:F0} {unitLabel} {compass}";
        }

        private string FormatTemperature(double temperature)
        {
            var converted = temperatureUnit == TemperatureUnit.Fahrenheit
                ? (temperature * 9 / 5) + 32
                : temperature;
            return $"{converted:F0}°";
        }

        private static string GetWindDirection(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees))
            {
                return string.Empty;
            }

            var normalized = (degrees % 360 + 360) % 360;
            string[] directions =
            [
                "N", "NNE", "NE", "ENE",
                "E", "ESE", "SE", "SSE",
                "S", "SSW", "SW", "WSW",
                "W", "WNW", "NW", "NNW"
            ];

            var index = (int)Math.Round(normalized / 22.5, MidpointRounding.AwayFromZero) % directions.Length;
            return directions[index];
        }

        private void ShowDetails(ForecastDay day)
        {
            selectedDayIndex = day.Index;
            UpdateDetails(day);

            overlayPanel.Visibility = Visibility.Visible;

            if (detailsCard.RenderTransform is TranslateTransform tt)
                tt.Y = detailsCard.MinHeight;

            DoubleAnimation fadeIn = new()
            {
                From = 0, To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, overlayPanel);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            DoubleAnimation slideUp = new()
            {
                From = detailsCard.MinHeight, To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }
            };
            Storyboard.SetTarget(slideUp, detailsCard.RenderTransform);
            Storyboard.SetTargetProperty(slideUp, "Y");

            Storyboard sb = new();
            sb.Children.Add(fadeIn);
            sb.Children.Add(slideUp);
            sb.Begin();
        }

        private void UpdateDetails(ForecastDay day)
        {
            detailsTitle.Text = day.FullDay;
            detailsHighLow.Text = $"H:{day.High}   L:{day.Low}";
            detailsCondValue.Text = day.Desc;
            detailsHumidValue.Text = day.Humidity;
            detailsWindValue.Text = day.Wind;
            detailsUVValue.Text = day.UV;
            detailsIcon.Text = day.Icon;
            detailsIcon.Foreground = new SolidColorBrush(day.IconColor);
        }

        private void HideDetails()
        {
            double cardH = detailsCard.ActualHeight > 0 ? detailsCard.ActualHeight : detailsCard.MinHeight;

            DoubleAnimation fadeOut = new()
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, overlayPanel);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            DoubleAnimation slideDown = new()
            {
                To = cardH,
                Duration = new Duration(TimeSpan.FromMilliseconds(280)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideDown, detailsCard.RenderTransform);
            Storyboard.SetTargetProperty(slideDown, "Y");

            Storyboard sb = new();
            sb.Children.Add(fadeOut);
            sb.Children.Add(slideDown);
            sb.Completed += (s, e) => overlayPanel.Visibility = Visibility.Collapsed;
            sb.Begin();
        }
    }
}
