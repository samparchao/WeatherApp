using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Drawing = System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using WeatherApp.Services;
using Windows.UI;
using Windows.Storage;
using WeatherApp.Elements;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Foundation.Collections;
using Windows.System;

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
        private Grid cityListContent;
        private StackPanel cityListStack;
        private Button mapToggleButton;
        private StackPanel cityDotsPanel;
        private Button cityListButton;
        private bool mapInitialized;

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

        private readonly List<TrackedCity> trackedCities = [];
        private readonly Dictionary<string, WeatherService.WeatherData> trackedCityWeather = new(StringComparer.Ordinal);
        private int activeTrackedCityIndex;
        private double forecastSwipeStartX;
        private bool forecastSwipeTracking;

        private AutoSuggestBox citySearchBox;
        private Button favoriteCityButton;
        private LocationService.CitySearchResult? pendingCitySelection;

        private const string TemperatureUnitSettingKey = "TemperatureUnit";
        private const string WindSpeedUnitSettingKey = "WindSpeedUnit";
        private const string TrackedCitiesSettingKey = "TrackedCities";

        private TrayIcon trayIcon;
        private bool allowClose;

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

        private record TrackedCity(string Name, double Latitude, double Longitude, bool IsFavorite);

        private record SavedTrackedCity(string Name, double Latitude, double Longitude);

        private record MapTrackedCity(string Name, double Latitude, double Longitude, bool IsFavorite, bool IsActive, double Temperature);

        private record CityPickerItem(Int32 Index, String Name, String Temperature, String Condition);

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
            PositionWindowBottomRight();

            LoadSettings();
            BuildUI();
            InitializeTrayIcon();
            _ = InitializeLocationAsync();
        }

        private async Task InitializeLocationAsync()
        {
            LocationService.LocationResult currentLocation;
            try
            {
                currentLocation = await LocationService.GetCurrentLocationAsync();
            }
            catch
            {
                locationText.Text = "Location unavailable";
                return;
            }

            EnsureCurrentLocationTracked(currentLocation);
            await LoadWeatherForActiveCityAsync();
        }

        private void EnsureCurrentLocationTracked(LocationService.LocationResult currentLocation)
        {
            String currentKey = GetCityKey(currentLocation.Latitude, currentLocation.Longitude);
            Int32 existingIndex = -1;

            for (Int32 i = 0; i < trackedCities.Count; i++)
            {
                TrackedCity trackedCity = trackedCities[i];
                String trackedKey = GetCityKey(trackedCity.Latitude, trackedCity.Longitude);
                if (string.Equals(trackedKey, currentKey, StringComparison.Ordinal))
                {
                    existingIndex = i;
                    break;
                }
            }

            Boolean isFavorite = false;
            if (existingIndex >= 0)
            {
                isFavorite = trackedCities[existingIndex].IsFavorite;
                trackedCities.RemoveAt(existingIndex);
            }

            TrackedCity currentTrackedCity = new TrackedCity(
                currentLocation.CityName,
                currentLocation.Latitude,
                currentLocation.Longitude,
                isFavorite);

            trackedCities.Insert(0, currentTrackedCity);
            activeTrackedCityIndex = 0;
        }

        private async Task LoadWeatherForActiveCityAsync()
        {
            if (trackedCities.Count == 0)
            {
                locationText.Text = "Location unavailable";
                return;
            }

            TrackedCity activeCity = trackedCities[activeTrackedCityIndex];
            locationText.Text = activeCity.Name;
            currentLatitude = activeCity.Latitude;
            currentLongitude = activeCity.Longitude;

            try
            {
                WeatherService.WeatherData weather = await WeatherService.GetWeatherAsync(activeCity.Latitude, activeCity.Longitude);
                trackedCityWeather[GetCityKey(activeCity.Latitude, activeCity.Longitude)] = weather;
                UpdateWeatherUI(weather);
            }
            catch
            {
                heroCondText.Text = "Weather unavailable";
            }

            RefreshCityNavigator();
            RefreshCityListPage();
            await SyncMapTrackedCitiesAsync();
        }

        private async Task AddPendingSelectionToFavoritesAsync()
        {
            if (pendingCitySelection == null)
            {
                return;
            }

            LocationService.CitySearchResult selectedCity = pendingCitySelection;
            await AddOrActivateTrackedCityAsync(selectedCity.CityName, selectedCity.Latitude, selectedCity.Longitude, true);

            citySearchBox.Text = string.Empty;
            citySearchBox.ItemsSource = Array.Empty<LocationService.CitySearchResult>();
            SetPendingCitySelection(null);
        }

        private async Task AddOrActivateTrackedCityAsync(string cityName, double latitude, double longitude, bool favorite)
        {
            String incomingKey = GetCityKey(latitude, longitude);
            Int32 existingIndex = -1;

            for (Int32 i = 0; i < trackedCities.Count; i++)
            {
                TrackedCity trackedCity = trackedCities[i];
                String trackedKey = GetCityKey(trackedCity.Latitude, trackedCity.Longitude);
                if (string.Equals(trackedKey, incomingKey, StringComparison.Ordinal))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                TrackedCity existingCity = trackedCities[existingIndex];
                trackedCities[existingIndex] = existingCity with
                {
                    Name = cityName,
                    IsFavorite = existingCity.IsFavorite || favorite
                };
                activeTrackedCityIndex = existingIndex;
            }
            else
            {
                trackedCities.Add(new TrackedCity(cityName, latitude, longitude, favorite));
                activeTrackedCityIndex = trackedCities.Count - 1;
            }

            RefreshCityNavigator();
            SaveSettings();
            await LoadWeatherForActiveCityAsync();
        }

        private void OnForecastPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Windows.Foundation.Point pointerPosition = e.GetCurrentPoint(forecastContent).Position;
            forecastSwipeStartX = pointerPosition.X;
            forecastSwipeTracking = true;
        }

        private async void OnForecastPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!forecastSwipeTracking)
            {
                return;
            }

            forecastSwipeTracking = false;
            Windows.Foundation.Point pointerPosition = e.GetCurrentPoint(forecastContent).Position;
            Double deltaX = pointerPosition.X - forecastSwipeStartX;
            if (Math.Abs(deltaX) < 64)
            {
                return;
            }

            Int32 offset = deltaX < 0 ? 1 : -1;
            await MoveTrackedCityByOffsetAsync(offset);
        }

        private async Task MoveTrackedCityByOffsetAsync(int offset)
        {
            if (trackedCities.Count <= 1)
            {
                return;
            }

            Int32 cityCount = trackedCities.Count;
            Int32 newIndex = (activeTrackedCityIndex + offset) % cityCount;
            if (newIndex < 0)
            {
                newIndex += cityCount;
            }

            activeTrackedCityIndex = newIndex;
            await LoadWeatherForActiveCityAsync();
        }

        private void UpdateWeatherUI(WeatherService.WeatherData weather)
        {
            currentWeather = weather;
            ApplyWeatherToUI(weather);
        }

        private void ApplyWeatherToUI(WeatherService.WeatherData weather)
        {
            WeatherService.CurrentWeather current = weather.Current;
            heroTemp.Text = FormatTemperature(current.Temperature);

            var (desc, icon, r, g, b) = WeatherService.MapWeatherCode(current.WeatherCode);
            heroCondIcon.Text = icon;
            heroCondIcon.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
            heroCondText.Text = desc;

            if (weather.Daily.Length > 0)
            {
                WeatherService.DailyForecast today = weather.Daily[0];
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

            Grid citySearchRow = new()
            {
                ColumnSpacing = 10,
                Margin = new Thickness(24, 10, 24, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 410
            };
            citySearchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            citySearchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            citySearchBox = new AutoSuggestBox
            {
                PlaceholderText = "Search city to track",
                MinWidth = 280,
                QueryIcon = new SymbolIcon(Symbol.Find),
                FontFamily = TextFont,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            citySearchBox.TextChanged += OnCitySearchTextChanged;
            citySearchBox.SuggestionChosen += OnCitySuggestionChosen;
            citySearchBox.QuerySubmitted += OnCityQuerySubmitted;
            Grid.SetColumn(citySearchBox, 0);
            citySearchRow.Children.Add(citySearchBox);

            favoriteCityButton = new Button
            {
                Content = "Favorite",
                IsEnabled = false,
                Height = 36,
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                FontFamily = TextFont,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.5)
            };
            favoriteCityButton.Click += OnFavoriteCityButtonClick;
            Grid.SetColumn(favoriteCityButton, 1);
            citySearchRow.Children.Add(favoriteCityButton);

            headerStack.Children.Add(citySearchRow);

            Grid.SetRow(headerStack, 0);
            rootGrid.Children.Add(headerStack);

            // Current weather hero
            StackPanel currentStack = new()
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

            // Content area with bottom city navigator
            Grid contentArea = new();
            contentArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Forecast view
            forecastContent = new Grid();
            forecastContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            forecastContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            forecastContent.PointerPressed += OnForecastPointerPressed;
            forecastContent.PointerReleased += OnForecastPointerReleased;

            TextBlock forecastHeader = new()
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

            Grid.SetRow(forecastContent, 0);
            contentArea.Children.Add(forecastContent);

            // Map view (initially hidden)
            mapWebView = new WebView2
            {
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(mapWebView, 0);
            contentArea.Children.Add(mapWebView);

            cityListContent = CreateCityListPage();
            Grid.SetRow(cityListContent, 0);
            contentArea.Children.Add(cityListContent);

            Border cityNavigator = CreateCityNavigator();
            Grid.SetRow(cityNavigator, 1);
            contentArea.Children.Add(cityNavigator);

            Grid.SetRow(contentArea, 3);
            rootGrid.Children.Add(contentArea);

            // Overlay & details sheet
            BuildOverlay(rootGrid);

            rootGrid.IsTabStop = true;
            rootGrid.KeyDown += OnMainWindowKeyDown;
            rootGrid.Loaded += (_, _) => rootGrid.Focus(FocusState.Programmatic);

            this.Content = rootGrid;
            RefreshCityNavigator();
        }

        private async void OnCitySearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            SetPendingCitySelection(null);

            String query = sender.Text?.Trim() ?? string.Empty;
            if (query.Length < 2)
            {
                sender.ItemsSource = Array.Empty<LocationService.CitySearchResult>();
                return;
            }

            try
            {
                IReadOnlyList<LocationService.CitySearchResult> suggestions = await LocationService.SearchCitiesAsync(query);
                sender.ItemsSource = suggestions;
            }
            catch
            {
                sender.ItemsSource = Array.Empty<LocationService.CitySearchResult>();
            }
        }

        private void OnCitySuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is not LocationService.CitySearchResult selectedCity)
            {
                return;
            }

            sender.Text = selectedCity.DisplayName;
            SetPendingCitySelection(selectedCity);
        }

        private void OnCityQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is LocationService.CitySearchResult selectedCity)
            {
                sender.Text = selectedCity.DisplayName;
                SetPendingCitySelection(selectedCity);
                return;
            }

            if (sender.ItemsSource is IReadOnlyList<LocationService.CitySearchResult> suggestions && suggestions.Count > 0)
            {
                LocationService.CitySearchResult fallbackCity = suggestions[0];
                sender.Text = fallbackCity.DisplayName;
                SetPendingCitySelection(fallbackCity);
            }
        }

        private async void OnFavoriteCityButtonClick(object sender, RoutedEventArgs e)
        {
            await AddPendingSelectionToFavoritesAsync();
        }

        private void SetPendingCitySelection(LocationService.CitySearchResult? selectedCity)
        {
            pendingCitySelection = selectedCity;
            if (favoriteCityButton == null)
            {
                return;
            }

            favoriteCityButton.IsEnabled = selectedCity != null;
            favoriteCityButton.Content = selectedCity == null ? "Favorite" : $"Favorite {selectedCity.CityName}";
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
                Grid card = new()
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

                TextBlock dayText = new()
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

                TextBlock icon = new()
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

                TextBlock desc = new()
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

                TextBlock high = new()
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

                TextBlock low = new()
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

            TextBlock label = new()
            {
                Text = $"{offLabel} / {onLabel}",
                FontSize = 15,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = DisplayFont
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            ToggleSwitch localToggle = new()
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

        private Border CreateCityNavigator()
        {
            Border container = new Border
            {
                Margin = new Thickness(18, 10, 18, 14),
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(20),
                Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0x16, 0x23, 0x40),
                    TintOpacity = 0.7,
                    FallbackColor = Color.FromArgb(0xB8, 0x14, 0x20, 0x38)
                },
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.6)
            };

            Grid grid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            mapToggleButton = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(12),
                FontFamily = TextFont,
                Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0)
            };
            mapToggleButton.Click += OnMapToggleButtonClick;
            Grid.SetColumn(mapToggleButton, 0);
            grid.Children.Add(mapToggleButton);

            cityDotsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8,
                Margin = new Thickness(8, 0, 8, 0)
            };

            ScrollViewer dotsScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollMode = ScrollMode.Disabled,
                Content = cityDotsPanel
            };
            dotsScroll.PointerWheelChanged += OnCityPointerWheelChanged;
            Grid.SetColumn(dotsScroll, 1);
            grid.Children.Add(dotsScroll);

            cityListButton = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Content = new SymbolIcon(Symbol.Bullets)
            };
            cityListButton.Click += OnCityListButtonClick;
            Grid.SetColumn(cityListButton, 2);
            grid.Children.Add(cityListButton);

            container.Child = grid;
            return container;
        }

        private Grid CreateCityListPage()
        {
            Grid citiesGrid = new Grid
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(20, 8, 20, 6)
            };
            citiesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            citiesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock title = new TextBlock
            {
                Text = "CITIES",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(12, 4, 12, 10),
                CharacterSpacing = 80,
                FontFamily = TextFont
            };
            Grid.SetRow(title, 0);
            citiesGrid.Children.Add(title);

            cityListStack = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            ScrollViewer listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = cityListStack
            };
            Grid.SetRow(listScroll, 1);
            citiesGrid.Children.Add(listScroll);

            return citiesGrid;
        }

        private void RefreshCityListPage()
        {
            if (cityListStack == null)
            {
                return;
            }

            cityListStack.Children.Clear();
            IReadOnlyList<CityPickerItem> items = BuildCityPickerItems();

            if (items.Count == 0)
            {
                TextBlock empty = new TextBlock
                {
                    Text = "No tracked cities yet.",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    Margin = new Thickness(12, 10, 12, 0)
                };
                cityListStack.Children.Add(empty);
                return;
            }

            foreach (CityPickerItem item in items)
            {
                cityListStack.Children.Add(CreateCityListRow(item));
            }
        }

        private Button CreateCityListRow(CityPickerItem item)
        {
            Grid rowGrid = new Grid
            {
                MinHeight = 62
            };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });

            StackPanel left = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            left.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontSize = 17,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = DisplayFont,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            left.Children.Add(new TextBlock
            {
                Text = item.Condition,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0x9A, 0xFF, 0xFF, 0xFF)),
                FontFamily = TextFont,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(left, 0);
            rowGrid.Children.Add(left);

            TextBlock right = new TextBlock
            {
                Text = item.Temperature,
                FontSize = 24,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = DisplayFont,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(right, 1);
            rowGrid.Children.Add(right);

            Button rowButton = new Button
            {
                Tag = item.Index,
                Height = 86,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                    TintOpacity = item.Index == activeTrackedCityIndex ? 0.16 : 0.08,
                    FallbackColor = item.Index == activeTrackedCityIndex
                        ? Color.FromArgb(0x36, 0xFF, 0xFF, 0xFF)
                        : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)
                },
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.5),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 12, 14, 12),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = rowGrid
            };
            rowButton.Click += OnCityListRowClick;
            return rowButton;
        }

        private void RefreshCityNavigator()
        {
            if (mapToggleButton != null)
            {
                Boolean isMapVisible = mapWebView != null && mapWebView.Visibility == Visibility.Visible;
                mapToggleButton.Content = new SymbolIcon(isMapVisible ? Symbol.BackToWindow : Symbol.Map);
                mapToggleButton.Background = new SolidColorBrush(
                    isMapVisible ? Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
            }

            if (cityListButton != null)
            {
                Boolean isCitiesPageVisible = cityListContent != null && cityListContent.Visibility == Visibility.Visible;
                cityListButton.Background = new SolidColorBrush(
                    isCitiesPageVisible ? Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
            }

            if (cityDotsPanel == null)
            {
                return;
            }

            cityDotsPanel.Children.Clear();

            for (Int32 i = 0; i < trackedCities.Count; i++)
            {
                Ellipse dotShape = new Ellipse
                {
                    Width = i == activeTrackedCityIndex ? 10 : 8,
                    Height = i == activeTrackedCityIndex ? 10 : 8,
                    Fill = new SolidColorBrush(i == activeTrackedCityIndex
                        ? Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF)
                        : Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF))
                };

                Button dotButton = new Button
                {
                    Tag = i,
                    Padding = new Thickness(0),
                    MinWidth = 14,
                    MinHeight = 14,
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Content = dotShape
                };
                dotButton.Click += OnCityDotButtonClick;
                cityDotsPanel.Children.Add(dotButton);
            }
        }

        private void SwitchToForecast()
        {
            if (forecastContent.Visibility == Visibility.Visible)
            {
                RefreshCityNavigator();
                return;
            }

            mapWebView.Visibility = Visibility.Collapsed;
            cityListContent.Visibility = Visibility.Collapsed;
            forecastContent.Visibility = Visibility.Visible;
            RefreshCityNavigator();

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
            if (mapWebView.Visibility == Visibility.Visible)
            {
                RefreshCityNavigator();
                return;
            }

            forecastContent.Visibility = Visibility.Collapsed;
            cityListContent.Visibility = Visibility.Collapsed;
            mapWebView.Visibility = Visibility.Visible;
            RefreshCityNavigator();

            if (!mapInitialized)
            {
                mapInitialized = true;
                await mapWebView.EnsureCoreWebView2Async();
                mapWebView.DefaultBackgroundColor = Color.FromArgb(0xFF, 0xE6, 0xEC, 0xF5);
                mapWebView.NavigationCompleted += OnMapNavigationCompleted;
                mapWebView.NavigateToString(WindowElements.GetMapHtml());
            }
            else
            {
                await SyncMapTrackedCitiesAsync();
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

        private void OnMapToggleButtonClick(object sender, RoutedEventArgs e)
        {
            if (mapWebView.Visibility == Visibility.Visible)
            {
                SwitchToForecast();
                return;
            }

            SwitchToMap();
        }

        private async void OnCityDotButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button cityButton || cityButton.Tag is not Int32 cityIndex)
            {
                return;
            }

            await SetActiveCityByIndexAsync(cityIndex);
        }

        private void OnCityListButtonClick(object sender, RoutedEventArgs e)
        {
            SwitchToCityList();
        }

        private void SwitchToCityList()
        {
            forecastContent.Visibility = Visibility.Collapsed;
            mapWebView.Visibility = Visibility.Collapsed;
            cityListContent.Visibility = Visibility.Visible;
            RefreshCityListPage();
            RefreshCityNavigator();

            Visual visual = ElementCompositionPreview.GetElementVisual(cityListContent);
            Compositor compositor = visual.Compositor;

            ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
            fade.Duration = TimeSpan.FromMilliseconds(260);
            visual.StartAnimation("Opacity", fade);
        }

        private async void OnCityListRowClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button cityButton || cityButton.Tag is not Int32 cityIndex)
            {
                return;
            }

            await SetActiveCityByIndexAsync(cityIndex);
        }

        private IReadOnlyList<CityPickerItem> BuildCityPickerItems()
        {
            List<CityPickerItem> items = new List<CityPickerItem>();
            for (Int32 i = 0; i < trackedCities.Count; i++)
            {
                TrackedCity trackedCity = trackedCities[i];
                String cityKey = GetCityKey(trackedCity.Latitude, trackedCity.Longitude);
                String temperature = "--";
                String condition = "Loading";

                if (trackedCityWeather.TryGetValue(cityKey, out WeatherService.WeatherData weatherData))
                {
                    temperature = FormatTemperature(weatherData.Current.Temperature);
                    (String description, String icon, Byte red, Byte green, Byte blue) = WeatherService.MapWeatherCode(weatherData.Current.WeatherCode);
                    condition = description;
                }

                String cityName = i == 0 ? $"{trackedCity.Name} (Current Location)" : trackedCity.Name;
                items.Add(new CityPickerItem(i, cityName, temperature, condition));
            }

            return items;
        }

        private async Task SetActiveCityByIndexAsync(Int32 cityIndex)
        {
            if (cityIndex < 0 || cityIndex >= trackedCities.Count)
            {
                return;
            }

            activeTrackedCityIndex = cityIndex;
            await LoadWeatherForActiveCityAsync();
            SwitchToForecast();
        }

        private async void OnMainWindowKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Right)
            {
                await MoveTrackedCityByOffsetAsync(1);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Left)
            {
                await MoveTrackedCityByOffsetAsync(-1);
                e.Handled = true;
            }
        }

        private async void OnCityPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            Int32 delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
            if (delta == 0)
            {
                return;
            }

            Int32 offset = delta < 0 ? 1 : -1;
            await MoveTrackedCityByOffsetAsync(offset);
            e.Handled = true;
        }

        private async void OnMapNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                return;
            }

            await SyncMapTrackedCitiesAsync();
        }

        private async Task SyncMapTrackedCitiesAsync()
        {
            if (!mapInitialized || mapWebView.CoreWebView2 == null)
            {
                return;
            }

            List<MapTrackedCity> mapCities = new List<MapTrackedCity>();
            for (Int32 i = 0; i < trackedCities.Count; i++)
            {
                TrackedCity trackedCity = trackedCities[i];
                String cityKey = GetCityKey(trackedCity.Latitude, trackedCity.Longitude);
                Double temperature = 0;

                if (trackedCityWeather.TryGetValue(cityKey, out WeatherService.WeatherData weatherData))
                {
                    temperature = weatherData.Current.Temperature;
                }

                mapCities.Add(new MapTrackedCity(
                    trackedCity.Name,
                    trackedCity.Latitude,
                    trackedCity.Longitude,
                    trackedCity.IsFavorite,
                    i == activeTrackedCityIndex,
                    temperature));
            }

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            String payload = JsonSerializer.Serialize(mapCities, jsonOptions);
            String script = $"window.setTrackedCities({payload});";
            await mapWebView.CoreWebView2.ExecuteScriptAsync(script);
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

            StackPanel cardContent = new StackPanel
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
            StackPanel titleRow = new()
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

        private void InitializeTrayIcon()
        {
            trayIcon = new TrayIcon
            {
                Icon = LoadTrayIcon(),
                ToolTipText = "Weather"
            };

            trayIcon.MenuItems.Add(new TrayMenuItem("Open", ShowFromTray));
            trayIcon.MenuItems.Add(new TrayMenuItem("Quit", QuitFromTray));

            trayIcon.LeftClick += (_, _) => ShowFromTray();

            this.AppWindow.Closing += OnAppWindowClosing;
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (allowClose)
            {
                return;
            }

            args.Cancel = true;
            sender.Hide();
        }

        private void ShowFromTray()
        {
            PositionWindowBottomRight();
            AppWindow.Show();
            Activate();
        }

        private void PositionWindowBottomRight()
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            RectInt32 workArea = displayArea.WorkArea;
            SizeInt32 size = AppWindow.Size;

            Int32 x = Math.Max(workArea.X, workArea.X + workArea.Width - size.Width);
            Int32 y = Math.Max(workArea.Y, workArea.Y + workArea.Height - size.Height);

            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }

        private void QuitFromTray()
        {
            allowClose = true;

            if (trayIcon != null)
            {
                trayIcon.Dispose();
                trayIcon = null;
            }

            Close();
        }

        private static Drawing.Icon LoadTrayIcon()
        {
            String path = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    return icon;
                }
            }

            return Drawing.SystemIcons.Application;
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
            IPropertySet settings = ApplicationData.Current.LocalSettings.Values;

            if (settings.TryGetValue(TemperatureUnitSettingKey, out Object tempValue) &&
                tempValue is string tempString &&
                Enum.TryParse(tempString, out TemperatureUnit storedTempUnit))
            {
                temperatureUnit = storedTempUnit;
            }

            if (settings.TryGetValue(WindSpeedUnitSettingKey, out Object windValue) &&
                windValue is string windString &&
                Enum.TryParse(windString, out WindSpeedUnit storedWindUnit))
            {
                windSpeedUnit = storedWindUnit;
            }

            LoadTrackedCitiesFromSettings(settings);
        }

        private void SaveSettings()
        {
            IPropertySet settings = ApplicationData.Current.LocalSettings.Values;
            settings[TemperatureUnitSettingKey] = temperatureUnit.ToString();
            settings[WindSpeedUnitSettingKey] = windSpeedUnit.ToString();
            SaveTrackedCitiesToSettings(settings);
        }

        private void LoadTrackedCitiesFromSettings(IPropertySet settings)
        {
            trackedCities.Clear();

            if (!settings.TryGetValue(TrackedCitiesSettingKey, out Object trackedValue) || trackedValue is not string trackedJson)
            {
                return;
            }

            try
            {
                SavedTrackedCity[]? savedCities = JsonSerializer.Deserialize<SavedTrackedCity[]>(trackedJson);
                if (savedCities == null)
                {
                    return;
                }

                foreach (SavedTrackedCity savedCity in savedCities)
                {
                    if (string.IsNullOrWhiteSpace(savedCity.Name))
                    {
                        continue;
                    }

                    trackedCities.Add(new TrackedCity(savedCity.Name, savedCity.Latitude, savedCity.Longitude, true));
                }
            }
            catch
            {
                trackedCities.Clear();
            }
        }

        private void SaveTrackedCitiesToSettings(IPropertySet settings)
        {
            List<SavedTrackedCity> savedCities = new List<SavedTrackedCity>();
            foreach (TrackedCity trackedCity in trackedCities)
            {
                if (!trackedCity.IsFavorite)
                {
                    continue;
                }

                savedCities.Add(new SavedTrackedCity(trackedCity.Name, trackedCity.Latitude, trackedCity.Longitude));
            }

            settings[TrackedCitiesSettingKey] = JsonSerializer.Serialize(savedCities);
        }

        private static string GetCityKey(double latitude, double longitude)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Math.Round(latitude, 4):F4}|{Math.Round(longitude, 4):F4}");
        }


        private string FormatWind(double speed, double direction)
        {
            String compass = GetWindDirection(direction);
            Double converted = windSpeedUnit == WindSpeedUnit.MilesPerHour
                ? speed * 0.621371
                : speed;
            String unitLabel = windSpeedUnit == WindSpeedUnit.MilesPerHour ? "mph" : "km/h";

            return string.IsNullOrWhiteSpace(compass)
                ? $"{converted:F0} {unitLabel}"
                : $"{converted:F0} {unitLabel} {compass}";
        }

        private string FormatTemperature(double temperature)
        {
            Double converted = temperatureUnit == TemperatureUnit.Fahrenheit
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

            Double normalized = (degrees % 360 + 360) % 360;
            string[] directions =
            [
                "N", "NNE", "NE", "ENE",
                "E", "ESE", "SE", "SSE",
                "S", "SSW", "SW", "WSW",
                "W", "WNW", "NW", "NNW"
            ];

            Int32 index = (int)Math.Round(normalized / 22.5, MidpointRounding.AwayFromZero) % directions.Length;
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
