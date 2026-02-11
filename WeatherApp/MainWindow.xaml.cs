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
using Windows.UI;

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
        private bool mapInitialized;

        private static readonly FontFamily WeatherIconsFont =
            new("ms-appx:///Assets/weathericons-regular-webfont.ttf#Weather Icons");
        private static readonly FontFamily DisplayFont = new("Segoe UI Variable Display");
        private static readonly FontFamily TextFont = new("Segoe UI Variable Text");

        private record ForecastDay(
            string Day, string FullDay, string Icon, Color IconColor,
            string High, string Low, string Desc,
            string Humidity, string Wind, string UV);

        private static readonly ForecastDay[] Forecast =
        [
            new("Mon", "Monday",    "\uF00D", Color.FromArgb(0xFF,0xFF,0xD4,0x5E), "23°","16°","Sunny",        "45%","8 km/h", "6"),
            new("Tue", "Tuesday",   "\uF002", Color.FromArgb(0xFF,0xB0,0xBE,0xCE), "21°","15°","Cloudy",       "62%","12 km/h","3"),
            new("Wed", "Wednesday", "\uF008", Color.FromArgb(0xFF,0x5B,0xC0,0xF8), "19°","14°","Rain",         "78%","18 km/h","2"),
            new("Thu", "Thursday",  "\uF002", Color.FromArgb(0xFF,0xF0,0xC8,0x5E), "22°","15°","Partly Cloudy","55%","10 km/h","5"),
            new("Fri", "Friday",    "\uF00D", Color.FromArgb(0xFF,0xFF,0xD4,0x5E), "24°","17°","Sunny",        "40%","6 km/h", "7"),
            new("Sat", "Saturday",  "\uF009", Color.FromArgb(0xFF,0x5B,0xC0,0xF8), "20°","13°","Showers",      "72%","15 km/h","2"),
            new("Sun", "Sunday",    "\uF010", Color.FromArgb(0xFF,0xF0,0x78,0x78), "18°","12°","Thunderstorm", "85%","22 km/h","1"),
        ];

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Weather";
            this.ExtendsContentIntoTitleBar = true;
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 780));

            BuildUI();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
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
            var headerStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0),
                Spacing = 2
            };

            headerStack.Children.Add(new TextBlock
            {
                Text = "San Francisco",
                FontSize = 34,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = DisplayFont
            });

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

            currentStack.Children.Add(new TextBlock
            {
                Text = "23°",
                FontSize = 100,
                FontWeight = FontWeights.Thin,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = DisplayFont,
                Margin = new Thickness(0, -8, 0, -16)
            });

            var condRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8
            };

            condRow.Children.Add(new TextBlock
            {
                Text = "\uF00D",
                FontFamily = WeatherIconsFont,
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xD4, 0x5E)),
                VerticalAlignment = VerticalAlignment.Center
            });

            condRow.Children.Add(new TextBlock
            {
                Text = "Sunny",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = TextFont
            });

            currentStack.Children.Add(condRow);

            currentStack.Children.Add(new TextBlock
            {
                Text = "H:24°  L:16°",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                FontFamily = TextFont
            });

            Grid.SetRow(currentStack, 1);
            rootGrid.Children.Add(currentStack);

            // Divider
            var divider = new Border
            {
                Height = 0.5,
                Margin = new Thickness(28, 20, 28, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF))
            };
            Grid.SetRow(divider, 2);
            rootGrid.Children.Add(divider);

            // Content area with tab selector
            var contentArea = new Grid();
            contentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tabSelector = CreateTabSelector();
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

            var forecastScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = CreateForecastPanel()
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

            Grid.SetRow(contentArea, 3);
            rootGrid.Children.Add(contentArea);

            // Overlay & details sheet
            BuildOverlay(rootGrid);

            this.Content = rootGrid;
        }

        private StackPanel CreateForecastPanel()
        {
            var panel = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(20, 0, 20, 24)
            };

            foreach (var day in Forecast)
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
            var outerBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4),
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                Padding = new Thickness(4)
            };

            var grid = new Grid();
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

            outerBorder.Child = grid;
            return outerBorder;
        }

        private void UpdateTabAppearance(bool isForecast)
        {
            forecastPill.Background = new SolidColorBrush(
                isForecast ? Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
            ((TextBlock)forecastPill.Child).Foreground = new SolidColorBrush(
                isForecast ? Colors.White : Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));

            mapPill.Background = new SolidColorBrush(
                !isForecast ? Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
            ((TextBlock)mapPill.Child).Foreground = new SolidColorBrush(
                !isForecast ? Colors.White : Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF));
        }

        private void SwitchToForecast()
        {
            if (forecastContent.Visibility == Visibility.Visible) return;

            mapWebView.Visibility = Visibility.Collapsed;
            forecastContent.Visibility = Visibility.Visible;
            UpdateTabAppearance(true);

            var visual = ElementCompositionPreview.GetElementVisual(forecastContent);
            var compositor = visual.Compositor;

            var fade = compositor.CreateScalarKeyFrameAnimation();
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
            mapWebView.Visibility = Visibility.Visible;
            UpdateTabAppearance(false);

            if (!mapInitialized)
            {
                mapInitialized = true;
                await mapWebView.EnsureCoreWebView2Async();
                mapWebView.DefaultBackgroundColor = Color.FromArgb(0xFF, 0x0D, 0x14, 0x2B);
                mapWebView.NavigateToString(GetMapHtml());
            }

            var visual = ElementCompositionPreview.GetElementVisual(mapWebView);
            var compositor = visual.Compositor;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0f);
            fade.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
            fade.Duration = TimeSpan.FromMilliseconds(300);
            visual.StartAnimation("Opacity", fade);
        }

        private static string GetMapHtml() => """
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width,initial-scale=1.0">
            <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
            <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
            <script src="https://unpkg.com/leaflet.heat@0.2.0/dist/leaflet-heat.js"></script>
            <style>
            *{margin:0;padding:0}
            body{background:#0D142B;overflow:hidden}
            #map{width:100vw;height:100vh}
            .leaflet-control-attribution{display:none!important}
            .leaflet-control-zoom a{
                background:rgba(20,32,58,0.85)!important;
                color:rgba(255,255,255,0.8)!important;
                border-color:rgba(255,255,255,0.12)!important;
            }
            .leaflet-control-zoom a:hover{
                background:rgba(40,55,90,0.9)!important;
                color:#fff!important;
            }
            .temp-marker{text-align:center;pointer-events:none}
            .temp-pill{
                display:inline-block;padding:3px 10px;border-radius:12px;
                color:#fff;font-size:13px;font-weight:600;
                font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;
                text-shadow:0 1px 2px rgba(0,0,0,0.4);
                box-shadow:0 2px 8px rgba(0,0,0,0.3);
            }
            .city-label{
                font-size:10px;color:rgba(255,255,255,0.55);margin-top:2px;
                font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;
                text-shadow:0 1px 3px rgba(0,0,0,0.6);
            }
            .legend{
                background:rgba(20,32,58,0.88)!important;
                border:0.5px solid rgba(255,255,255,0.12);
                border-radius:14px;padding:14px 18px;
                color:#fff;backdrop-filter:blur(20px);
                font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;
            }
            .legend h4{
                margin:0 0 8px 0;font-size:10px;font-weight:600;
                text-transform:uppercase;letter-spacing:1.4px;opacity:0.6;
            }
            .grad-bar{
                width:150px;height:6px;border-radius:3px;
                background:linear-gradient(to right,#313695,#4575b4,#91bfdb,#e0f3f8,#fee090,#fdae61,#f46d43,#d73027,#a50026);
                margin-bottom:5px;
            }
            .grad-labels{display:flex;justify-content:space-between;font-size:9px;opacity:0.5}
            </style>
            </head>
            <body>
            <div id="map"></div>
            <script>
            var map=L.map('map',{center:[25,0],zoom:2,zoomControl:false,attributionControl:false});
            L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',{maxZoom:18}).addTo(map);
            L.control.zoom({position:'topright'}).addTo(map);

            var heat=L.heatLayer([
                [23.4,53.8,1],[25.2,55.3,.95],[24.7,46.7,.98],[30,31.2,.85],
                [28.6,77.2,.9],[19.1,72.9,.82],[13.1,80.3,.88],[1.3,103.8,.8],
                [14.6,121,.78],[-6.2,106.8,.75],[3.1,101.7,.77],[13.8,100.5,.8],
                [21,105.9,.72],[-23.5,-46.6,.6],[-22.9,-43.2,.62],
                [33.9,-118.2,.55],[25.8,-80.2,.72],[29.8,-95.4,.7],
                [33.4,-112,.85],[36.2,-115.1,.82],[37.8,-122.4,.48],
                [35.7,139.7,.55],[37.6,127,.5],[39.9,116.4,.52],[31.2,121.5,.58],
                [22.3,114.2,.7],[41,29,.6],[40.4,-3.7,.58],[41.4,2.2,.55],
                [37.9,23.7,.62],[38.7,-9.1,.52],
                [48.9,2.3,.4],[51.5,-.1,.35],[52.5,13.4,.38],[48.1,11.6,.36],
                [45.5,9.2,.42],[41.9,12.5,.48],[47.4,8.5,.34],
                [40.7,-74,.42],[42.4,-71.1,.38],[41.9,-87.6,.4],
                [43.7,-79.4,.36],[45.5,-73.6,.32],[49.3,-123.1,.32],[47.6,-122.3,.35],
                [-33.9,151.2,.45],[-37.8,145,.38],[-36.8,174.8,.35],
                [55.8,37.6,.2],[59.9,30.3,.15],[59.3,18.1,.22],[60.2,24.9,.18],
                [55.7,12.6,.25],[64.1,-21.9,.1],[69.6,18.9,.05],
                [61.2,-150,.08],[51.2,-114.1,.2],[53.5,-113.5,.18]
            ],{radius:35,blur:40,maxZoom:10,max:1,gradient:{
                0:'#313695',.15:'#4575b4',.3:'#74add1',.4:'#abd9e9',
                .5:'#e0f3f8',.6:'#fee090',.7:'#fdae61',.8:'#f46d43',
                .9:'#d73027',1:'#a50026'}}).addTo(map);

            var cities=[
                {n:'San Francisco',lat:37.8,lng:-122.4,t:23},
                {n:'New York',lat:40.7,lng:-74,t:18},
                {n:'London',lat:51.5,lng:-.1,t:14},
                {n:'Tokyo',lat:35.7,lng:139.7,t:22},
                {n:'Sydney',lat:-33.9,lng:151.2,t:19},
                {n:'Dubai',lat:25.2,lng:55.3,t:38},
                {n:'Moscow',lat:55.8,lng:37.6,t:5},
                {n:'S\u00e3o Paulo',lat:-23.5,lng:-46.6,t:26},
                {n:'Cairo',lat:30,lng:31.2,t:34},
                {n:'Paris',lat:48.9,lng:2.3,t:16},
                {n:'Mumbai',lat:19.1,lng:72.9,t:32},
                {n:'Beijing',lat:39.9,lng:116.4,t:20}
            ];
            cities.forEach(function(c){
                var col=c.t>32?'#a50026':c.t>26?'#d73027':c.t>20?'#f46d43':c.t>14?'#fdae61':c.t>8?'#abd9e9':c.t>0?'#74add1':'#4575b4';
                L.marker([c.lat,c.lng],{icon:L.divIcon({className:'temp-marker',
                    html:'<div class="temp-pill" style="background:'+col+'">'+c.t+'\u00b0</div><div class="city-label">'+c.n+'</div>',
                    iconSize:[90,42],iconAnchor:[45,21]})}).addTo(map);
            });

            var legend=L.control({position:'bottomleft'});
            legend.onAdd=function(){
                var d=L.DomUtil.create('div','legend');
                d.innerHTML='<h4>Temperature</h4><div class="grad-bar"></div><div class="grad-labels"><span>-10\u00b0</span><span>10\u00b0</span><span>25\u00b0</span><span>40\u00b0+</span></div>';
                return d;
            };
            legend.addTo(map);
            </script>
            </body>
            </html>
            """;

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
            var detailsGrid = new Grid();
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var condItem = CreateDetailItem("Condition", out detailsCondValue);
            Grid.SetRow(condItem, 0); Grid.SetColumn(condItem, 0);
            detailsGrid.Children.Add(condItem);

            var humidItem = CreateDetailItem("Humidity", out detailsHumidValue);
            Grid.SetRow(humidItem, 0); Grid.SetColumn(humidItem, 1);
            detailsGrid.Children.Add(humidItem);

            var windItem = CreateDetailItem("Wind", out detailsWindValue);
            Grid.SetRow(windItem, 1); Grid.SetColumn(windItem, 0);
            detailsGrid.Children.Add(windItem);

            var uvItem = CreateDetailItem("UV Index", out detailsUVValue);
            Grid.SetRow(uvItem, 1); Grid.SetColumn(uvItem, 1);
            detailsGrid.Children.Add(uvItem);

            cardContent.Children.Add(detailsGrid);

            // Dismiss button
            var closeBtn = new Button
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
            var stack = new StackPanel
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

        private void ShowDetails(ForecastDay day)
        {
            detailsTitle.Text = day.FullDay;
            detailsHighLow.Text = $"H:{day.High}   L:{day.Low}";
            detailsCondValue.Text = day.Desc;
            detailsHumidValue.Text = day.Humidity;
            detailsWindValue.Text = day.Wind;
            detailsUVValue.Text = day.UV;
            detailsIcon.Text = day.Icon;
            detailsIcon.Foreground = new SolidColorBrush(day.IconColor);

            overlayPanel.Visibility = Visibility.Visible;

            if (detailsCard.RenderTransform is TranslateTransform tt)
                tt.Y = detailsCard.MinHeight;

            var fadeIn = new DoubleAnimation
            {
                From = 0, To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, overlayPanel);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            var slideUp = new DoubleAnimation
            {
                From = detailsCard.MinHeight, To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }
            };
            Storyboard.SetTarget(slideUp, detailsCard.RenderTransform);
            Storyboard.SetTargetProperty(slideUp, "Y");

            var sb = new Storyboard();
            sb.Children.Add(fadeIn);
            sb.Children.Add(slideUp);
            sb.Begin();
        }

        private void HideDetails()
        {
            double cardH = detailsCard.ActualHeight > 0 ? detailsCard.ActualHeight : detailsCard.MinHeight;

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, overlayPanel);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            var slideDown = new DoubleAnimation
            {
                To = cardH,
                Duration = new Duration(TimeSpan.FromMilliseconds(280)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideDown, detailsCard.RenderTransform);
            Storyboard.SetTargetProperty(slideDown, "Y");

            var sb = new Storyboard();
            sb.Children.Add(fadeOut);
            sb.Children.Add(slideDown);
            sb.Completed += (s, e) => overlayPanel.Visibility = Visibility.Collapsed;
            sb.Begin();
        }
    }
}
