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
        private TextBlock detailsCloudValue;
        private TextBlock detailsSunriseValue;
        private TextBlock detailsSunsetValue;
        private TextBlock detailsGoldenHourValue;
        private TextBlock detailsBlueHourValue;
        private TextBlock detailsFeelsLikeValue;
        private TextBlock detailsDewPointValue;
        private TextBlock detailsPressureTrendValue;
        private TextBlock detailsAqiValue;
        private TextBlock detailsRainWindowValue;
        private StackPanel detailsTemperatureTimelinePanel;
        private StackPanel detailsGustTimelinePanel;
        private StackPanel detailsRainTimelinePanel;
        private TextBlock detailsIcon;

        private WebView2 mapWebView;
        private Grid forecastContent;
        private Grid cityListContent;
        private StackPanel cityListStack;
        private Grid astroContent;
        private StackPanel astroTargetsStack;
        private TextBlock astroPageTitle;
        private TextBlock astroSummaryText;
        private TextBlock astroCoordinatesText;
        private TextBlock astroBestWindowText;
        private TextBlock astroExtraText;
        private Button astroExpandButton;
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
        private static readonly String[] WindDirectionLabels =
        [
            "N", "NNE", "NE", "ENE",
            "E", "ESE", "SE", "SSE",
            "S", "SSW", "SW", "WSW",
            "W", "WNW", "NW", "NNW"
        ];
        private int activeTrackedCityIndex;
        private double forecastSwipeStartX;
        private bool forecastSwipeTracking;

        private AutoSuggestBox citySearchBox;
        private Button favoriteCityButton;
        private LocationService.CitySearchResult? pendingCitySelection;

        private readonly List<AstroService.AstroTargetInfo> astroTargets = [];
        private Int32 selectedAstroTargetIndex = -1;
        private DateTime astroForecastDate;
        private Boolean astroExpanded;
        private Boolean astroTargetsLoading;

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

        private record HourlyRainPoint(string HourLabel, double ProbabilityPercent, double AmountMillimeters);

        private record HourlyMetricPoint(string HourLabel, double Value);

        private record ForecastDay(
            int Index,
            string Day,
            string FullDay,
            string Icon,
            Color IconColor,
            string High,
            string Low,
            string Desc,
            string Humidity,
            string Wind,
            string UV,
            string CloudCover,
            string RainWindow,
            string FeelsLike,
            string DewPoint,
            string PressureTrend,
            string AirQuality,
            DateTime Sunrise,
            DateTime Sunset,
            string Daylight,
            HourlyRainPoint[] HourlyRain,
            HourlyMetricPoint[] HourlyTemperature,
            HourlyMetricPoint[] HourlyWindGust);

        private record TrackedCity(string Name, double Latitude, double Longitude, bool IsFavorite);

        private record SavedTrackedCity(string Name, double Latitude, double Longitude);

        private record MapTrackedCity(string Name, double Latitude, double Longitude, bool IsFavorite, bool IsActive, double Temperature);

        private record CityPickerItem(Int32 Index, String Name, String Temperature, String Condition);

        private record AstroTargetListItem(Int32 Index, AstroService.AstroTargetInfo Target);

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
            await EnsureTrackedCitiesWeatherAsync();
        }

        private async Task EnsureTrackedCitiesWeatherAsync()
        {
            Boolean updated = false;

            for (Int32 i = 0; i < trackedCities.Count; i++)
            {
                TrackedCity trackedCity = trackedCities[i];
                String cityKey = GetCityKey(trackedCity.Latitude, trackedCity.Longitude);
                if (trackedCityWeather.ContainsKey(cityKey))
                {
                    continue;
                }

                try
                {
                    WeatherService.WeatherData weather = await WeatherService.GetWeatherAsync(trackedCity.Latitude, trackedCity.Longitude);
                    trackedCityWeather[cityKey] = weather;
                    updated = true;
                }
                catch
                {
                }
            }

            if (!updated)
            {
                return;
            }

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

            ForecastDay[] forecastDays = new ForecastDay[weather.Daily.Length];
            for (Int32 i = 0; i < weather.Daily.Length; i++)
            {
                WeatherService.DailyForecast d = weather.Daily[i];
                (String dayDesc, String dayIcon, Byte dr, Byte dg, Byte db) = WeatherService.MapWeatherCode(d.WeatherCode);
                String dayName = d.Date.Date == DateTime.Today ? "Today" : d.Date.ToString("ddd");
                String fullDay = d.Date.Date == DateTime.Today ? "Today" : d.Date.ToString("dddd");

                List<HourlyRainPoint> rainPoints = new List<HourlyRainPoint>();
                List<HourlyMetricPoint> temperaturePoints = new List<HourlyMetricPoint>();
                List<HourlyMetricPoint> gustPoints = new List<HourlyMetricPoint>();
                List<Double> pressureValues = new List<Double>();
                List<Int32> aqiValues = new List<Int32>();

                Double cloudCoverTotal = 0;
                Double feelsLikeTotal = 0;
                Double dewPointTotal = 0;
                Int32 weatherSamples = 0;

                DateTime? rainStart = null;
                DateTime? rainEnd = null;

                foreach (WeatherService.HourlyForecast hourly in weather.Hourly)
                {
                    if (hourly.Time.Date != d.Date.Date)
                    {
                        continue;
                    }

                    String hourLabel = hourly.Time.ToString("HH:mm");
                    rainPoints.Add(new HourlyRainPoint(
                        hourLabel,
                        hourly.PrecipitationProbability,
                        hourly.Precipitation));

                    temperaturePoints.Add(new HourlyMetricPoint(hourLabel, hourly.Temperature));
                    gustPoints.Add(new HourlyMetricPoint(hourLabel, hourly.WindGust));
                    pressureValues.Add(hourly.SurfacePressure);

                    if (hourly.AirQualityIndex.HasValue)
                    {
                        aqiValues.Add(hourly.AirQualityIndex.Value);
                    }

                    if (hourly.PrecipitationProbability >= 40 || hourly.Precipitation >= 0.2)
                    {
                        if (rainStart == null)
                        {
                            rainStart = hourly.Time;
                        }

                        rainEnd = hourly.Time;
                    }

                    cloudCoverTotal += hourly.CloudCover;
                    feelsLikeTotal += hourly.ApparentTemperature;
                    dewPointTotal += hourly.DewPoint;
                    weatherSamples++;
                }

                String cloudCoverageText = weatherSamples == 0
                    ? "--"
                    : $"{Math.Round(cloudCoverTotal / weatherSamples):F0}%";

                String rainWindowText = "No significant rain expected";
                if (rainStart != null && rainEnd != null)
                {
                    rainWindowText = rainStart == rainEnd
                        ? $"Rain likely near {rainStart:HH:mm}"
                        : $"Rain likely {rainStart:HH:mm} - {rainEnd:HH:mm}";
                }

                String feelsLikeText = weatherSamples == 0
                    ? "--"
                    : FormatTemperature(feelsLikeTotal / weatherSamples);

                String dewPointText = weatherSamples == 0
                    ? "--"
                    : FormatTemperature(dewPointTotal / weatherSamples);

                String pressureTrendText = "Stable";
                if (pressureValues.Count >= 2)
                {
                    Double pressureDelta = pressureValues[^1] - pressureValues[0];
                    if (pressureDelta > 1.5)
                    {
                        pressureTrendText = $"Rising (+{pressureDelta:F1} hPa)";
                    }
                    else if (pressureDelta < -1.5)
                    {
                        pressureTrendText = $"Falling ({pressureDelta:F1} hPa)";
                    }
                    else
                    {
                        pressureTrendText = $"Stable ({pressureDelta:+0.0;-0.0;0.0} hPa)";
                    }
                }

                String airQualityText;
                if (aqiValues.Count == 0)
                {
                    airQualityText = "AQI unavailable";
                }
                else
                {
                    Double avgAqi = 0;
                    foreach (Int32 value in aqiValues)
                    {
                        avgAqi += value;
                    }

                    avgAqi /= aqiValues.Count;
                    airQualityText = DescribeAirQuality((Int32)Math.Round(avgAqi));
                }

                String daylightText = $"{d.DaylightDurationHours:F1}h";

                forecastDays[i] = new ForecastDay(
                    i,
                    dayName,
                    fullDay,
                    dayIcon,
                    Color.FromArgb(0xFF, dr, dg, db),
                    FormatTemperature(d.TempMax),
                    FormatTemperature(d.TempMin),
                    dayDesc,
                    $"{d.Humidity:F0}%",
                    FormatWind(d.WindSpeed, d.WindDirection),
                    $"{d.UVIndex:F0}",
                    cloudCoverageText,
                    rainWindowText,
                    feelsLikeText,
                    dewPointText,
                    pressureTrendText,
                    airQualityText,
                    d.Sunrise,
                    d.Sunset,
                    daylightText,
                    rainPoints.ToArray(),
                    temperaturePoints.ToArray(),
                    gustPoints.ToArray());
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

            astroContent = CreateAstroPage();
            Grid.SetRowSpan(astroContent, 4);
            Canvas.SetZIndex(astroContent, 20);
            rootGrid.Children.Add(astroContent);

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

            cityListStack.Children.Add(new TextBlock
            {
                Text = "SETTINGS",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                CharacterSpacing = 80,
                FontFamily = TextFont,
                Margin = new Thickness(12, 16, 12, 6)
            });
            cityListStack.Children.Add(CreateSettingsPanel());
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

            ShowPrimaryPage(forecastContent, 300);
        }

        private async void SwitchToMap()
        {
            if (mapWebView.Visibility == Visibility.Visible)
            {
                RefreshCityNavigator();
                return;
            }

            ShowPrimaryPage(mapWebView, 300);

            if (!mapInitialized)
            {
                mapInitialized = true;
                await mapWebView.EnsureCoreWebView2Async();
                mapWebView.DefaultBackgroundColor = Color.FromArgb(0xFF, 0xE6, 0xEC, 0xF5);
                mapWebView.NavigationCompleted += OnMapNavigationCompleted;
                mapWebView.NavigateToString(WindowElements.GetMapHtml());
            }

            await EnsureTrackedCitiesWeatherAsync();
            await SyncMapTrackedCitiesAsync();
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

        private async void OnCityListButtonClick(object sender, RoutedEventArgs e)
        {
            await SwitchToCityListAsync();
        }

        private async Task SwitchToCityListAsync()
        {
            ShowPrimaryPage(cityListContent, 260);
            RefreshCityListPage();
            await EnsureTrackedCitiesWeatherAsync();
        }

        private void ShowPrimaryPage(UIElement page, Int32 fadeDurationMs)
        {
            if (page == null)
            {
                return;
            }

            forecastContent.Visibility = page == forecastContent ? Visibility.Visible : Visibility.Collapsed;
            mapWebView.Visibility = page == mapWebView ? Visibility.Visible : Visibility.Collapsed;
            cityListContent.Visibility = page == cityListContent ? Visibility.Visible : Visibility.Collapsed;
            astroContent.Visibility = page == astroContent ? Visibility.Visible : Visibility.Collapsed;

            RefreshCityNavigator();
            AnimateFadeIn(page, fadeDurationMs);
        }

        private static void AnimateFadeIn(UIElement target, Int32 durationMs)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(target);
            Compositor compositor = visual.Compositor;

            ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
            fade.Duration = TimeSpan.FromMilliseconds(durationMs);
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
                    (String description, _, _, _, _) = WeatherService.MapWeatherCode(weatherData.Current.WeatherCode);
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
            Grid titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            detailsIcon = new TextBlock
            {
                FontFamily = WeatherIconsFont,
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(detailsIcon, 0);
            titleRow.Children.Add(detailsIcon);

            detailsTitle = new TextBlock
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = DisplayFont,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(detailsTitle, 1);
            titleRow.Children.Add(detailsTitle);

            Button astroQuickButton = new Button
            {
                Width = 34,
                Height = 34,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.5),
                Content = new SymbolIcon(Symbol.Globe)
                {
                    Foreground = new SolidColorBrush(Colors.White)
                }
};
            ToolTipService.SetToolTip(astroQuickButton, "Open Astro Targets");
            astroQuickButton.Click += OnOpenAstroTargetsClick;
            Grid.SetColumn(astroQuickButton, 2);
            titleRow.Children.Add(astroQuickButton);

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

            // Detail grid
            Grid detailsGrid = new();
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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

            StackPanel cloudItem = CreateDetailItem("Cloud Cover", out detailsCloudValue);
            Grid.SetRow(cloudItem, 2); Grid.SetColumn(cloudItem, 0);
            Grid.SetColumnSpan(cloudItem, 2);
            detailsGrid.Children.Add(cloudItem);

            cardContent.Children.Add(detailsGrid);

            Grid sunGrid = new Grid
            {
                Margin = new Thickness(0, 4, 0, 0)
            };
            sunGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sunGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sunGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sunGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel sunriseItem = CreateDetailItem("Sunrise", out detailsSunriseValue);
            Grid.SetRow(sunriseItem, 0); Grid.SetColumn(sunriseItem, 0);
            sunGrid.Children.Add(sunriseItem);

            StackPanel sunsetItem = CreateDetailItem("Sunset", out detailsSunsetValue);
            Grid.SetRow(sunsetItem, 0); Grid.SetColumn(sunsetItem, 1);
            sunGrid.Children.Add(sunsetItem);

            StackPanel goldenHourItem = CreateDetailItem("Golden Hour", out detailsGoldenHourValue);
            Grid.SetRow(goldenHourItem, 1); Grid.SetColumn(goldenHourItem, 0);
            sunGrid.Children.Add(goldenHourItem);

            StackPanel blueHourItem = CreateDetailItem("Blue Hour", out detailsBlueHourValue);
            Grid.SetRow(blueHourItem, 1); Grid.SetColumn(blueHourItem, 1);
            sunGrid.Children.Add(blueHourItem);

            cardContent.Children.Add(sunGrid);

            Grid comfortGrid = new Grid
            {
                Margin = new Thickness(0, 4, 0, 0)
            };
            comfortGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comfortGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comfortGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            comfortGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            comfortGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel feelsLikeItem = CreateDetailItem("Feels Like", out detailsFeelsLikeValue);
            Grid.SetRow(feelsLikeItem, 0); Grid.SetColumn(feelsLikeItem, 0);
            comfortGrid.Children.Add(feelsLikeItem);

            StackPanel dewPointItem = CreateDetailItem("Dew Point", out detailsDewPointValue);
            Grid.SetRow(dewPointItem, 0); Grid.SetColumn(dewPointItem, 1);
            comfortGrid.Children.Add(dewPointItem);

            StackPanel pressureItem = CreateDetailItem("Pressure Trend", out detailsPressureTrendValue);
            Grid.SetRow(pressureItem, 1); Grid.SetColumn(pressureItem, 0);
            comfortGrid.Children.Add(pressureItem);

            StackPanel aqiItem = CreateDetailItem("Air Quality", out detailsAqiValue);
            Grid.SetRow(aqiItem, 1); Grid.SetColumn(aqiItem, 1);
            comfortGrid.Children.Add(aqiItem);

            StackPanel rainWindowItem = CreateDetailItem("Rain Window", out detailsRainWindowValue);
            Grid.SetRow(rainWindowItem, 2); Grid.SetColumn(rainWindowItem, 0);
            Grid.SetColumnSpan(rainWindowItem, 2);
            comfortGrid.Children.Add(rainWindowItem);

            cardContent.Children.Add(comfortGrid);

            cardContent.Children.Add(CreateSectionHeader("HOURLY TEMPERATURE"));

            detailsTemperatureTimelinePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };

            ScrollViewer temperatureScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollMode = ScrollMode.Disabled,
                Content = detailsTemperatureTimelinePanel,
                MinHeight = 124
            };
            cardContent.Children.Add(temperatureScroll);

            cardContent.Children.Add(CreateSectionHeader("WIND GUSTS"));

            detailsGustTimelinePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };

            ScrollViewer gustScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollMode = ScrollMode.Disabled,
                Content = detailsGustTimelinePanel,
                MinHeight = 124
            };
            cardContent.Children.Add(gustScroll);

            cardContent.Children.Add(CreateSectionHeader("HOURLY RAIN"));

            detailsRainTimelinePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };

            ScrollViewer rainScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollMode = ScrollMode.Disabled,
                Content = detailsRainTimelinePanel,
                MinHeight = 122
            };
            cardContent.Children.Add(rainScroll);

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

            ScrollViewer detailsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollMode = ScrollMode.Enabled,
                HorizontalScrollMode = ScrollMode.Disabled,
                Content = cardContent
            };

            detailsCard = new Border
            {
                CornerRadius = new CornerRadius(20, 20, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MaxWidth = 460,
                MaxHeight = 520,
                MinHeight = 300,
                Margin = new Thickness(0, 48, 0, 0),
                RenderTransform = new TranslateTransform { Y = 500 },
                Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0x14, 0x20, 0x3A),
                    TintOpacity = 0.85,
                    FallbackColor = Color.FromArgb(0xF0, 0x1A, 0x2A, 0x48)
                },
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.5, 0.5, 0.5, 0),
                Child = detailsScroll
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

        private static TextBlock CreateSectionHeader(String text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                CharacterSpacing = 60,
                FontFamily = TextFont,
                Margin = new Thickness(0, 6, 0, 2)
            };
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

        private static string DescribeAirQuality(Int32 aqi)
        {
            String category = aqi switch
            {
                <= 50 => "Good",
                <= 100 => "Moderate",
                <= 150 => "Unhealthy (Sensitive)",
                <= 200 => "Unhealthy",
                <= 300 => "Very Unhealthy",
                _ => "Hazardous"
            };

            return $"{aqi} ({category})";
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
            Int32 index = (int)Math.Round(normalized / 22.5, MidpointRounding.AwayFromZero) % WindDirectionLabels.Length;
            return WindDirectionLabels[index];
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
            detailsCloudValue.Text = day.CloudCover;
            detailsSunriseValue.Text = day.Sunrise.ToString("HH:mm");
            detailsSunsetValue.Text = day.Sunset.ToString("HH:mm");
            detailsGoldenHourValue.Text = $"{day.Sunrise.AddMinutes(-30):HH:mm} - {day.Sunrise.AddMinutes(60):HH:mm}";
            detailsBlueHourValue.Text = $"{day.Sunset.AddMinutes(-50):HH:mm} - {day.Sunset.AddMinutes(-10):HH:mm}";
            detailsFeelsLikeValue.Text = day.FeelsLike;
            detailsDewPointValue.Text = day.DewPoint;
            detailsPressureTrendValue.Text = day.PressureTrend;
            detailsAqiValue.Text = day.AirQuality;
            detailsRainWindowValue.Text = day.RainWindow;
            detailsIcon.Text = day.Icon;
            detailsIcon.Foreground = new SolidColorBrush(day.IconColor);
            RenderHourlyMetricTimeline(detailsTemperatureTimelinePanel, day.HourlyTemperature, 0xFF, 0xF2, 0xA4, isTemperature: true);
            RenderHourlyMetricTimeline(detailsGustTimelinePanel, day.HourlyWindGust, 0x8E, 0xD7, 0xFF, isTemperature: false);
            RenderHourlyRainTimeline(day.HourlyRain);
        }

        private void RenderHourlyMetricTimeline(StackPanel targetPanel, HourlyMetricPoint[] points, Byte red, Byte green, Byte blue, Boolean isTemperature)
        {
            if (targetPanel == null)
            {
                return;
            }

            targetPanel.Children.Clear();

            if (points == null || points.Length == 0)
            {
                targetPanel.Children.Add(new TextBlock
                {
                    Text = "No hourly data.",
                    Foreground = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    FontSize = 12,
                    Margin = new Thickness(4, 8, 0, 0)
                });
                return;
            }

            Int32 sampledCount = Math.Min(12, points.Length);
            Int32 step = Math.Max(1, points.Length / sampledCount);

            Double minValue = Double.MaxValue;
            Double maxValue = Double.MinValue;
            for (Int32 i = 0; i < points.Length; i++)
            {
                minValue = Math.Min(minValue, points[i].Value);
                maxValue = Math.Max(maxValue, points[i].Value);
            }

            Double span = Math.Max(0.1, maxValue - minValue);

            for (Int32 i = 0; i < points.Length; i += step)
            {
                HourlyMetricPoint point = points[i];
                Double normalized = (point.Value - minValue) / span;
                Double barHeight = 8 + normalized * 52;

                StackPanel column = new StackPanel
                {
                    Width = 40,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 4
                };

                column.Children.Add(new TextBlock
                {
                    Text = point.HourLabel,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x85, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                Border bar = new Border
                {
                    Height = barHeight,
                    Width = 20,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(0xC8, red, green, blue)),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                column.Children.Add(bar);

                String valueText = isTemperature
                    ? FormatTemperature(point.Value)
                    : $"{point.Value:F0}";

                column.Children.Add(new TextBlock
                {
                    Text = valueText,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                targetPanel.Children.Add(column);
            }
        }

        private void RenderHourlyRainTimeline(HourlyRainPoint[] rainPoints)
        {
            if (detailsRainTimelinePanel == null)
            {
                return;
            }

            detailsRainTimelinePanel.Children.Clear();

            if (rainPoints == null || rainPoints.Length == 0)
            {
                detailsRainTimelinePanel.Children.Add(new TextBlock
                {
                    Text = "No hourly precipitation data.",
                    Foreground = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    FontSize = 12,
                    Margin = new Thickness(4, 8, 0, 0)
                });
                return;
            }

            Int32 sampledCount = Math.Min(12, rainPoints.Length);
            Int32 step = Math.Max(1, rainPoints.Length / sampledCount);

            for (Int32 i = 0; i < rainPoints.Length; i += step)
            {
                HourlyRainPoint point = rainPoints[i];
                Double clampedProbability = Math.Clamp(point.ProbabilityPercent, 0, 100);
                Double barHeight = 8 + (clampedProbability / 100.0) * 52;

                StackPanel column = new StackPanel
                {
                    Width = 36,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 4
                };

                column.Children.Add(new TextBlock
                {
                    Text = point.HourLabel,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x85, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                Border bar = new Border
                {
                    Height = barHeight,
                    Width = 20,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(0xC8, 0x6B, 0xB9, 0xFF)),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                column.Children.Add(bar);

                column.Children.Add(new TextBlock
                {
                    Text = $"{clampedProbability:F0}%",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                detailsRainTimelinePanel.Children.Add(column);
            }
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
