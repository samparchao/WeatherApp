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

            // Forecast section
            var forecastSection = new StackPanel();

            forecastSection.Children.Add(new TextBlock
            {
                Text = "7-DAY FORECAST",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(32, 16, 32, 8),
                CharacterSpacing = 80,
                FontFamily = TextFont
            });

            forecastSection.Children.Add(new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = CreateForecastPanel()
            });

            Grid.SetRow(forecastSection, 3);
            rootGrid.Children.Add(forecastSection);

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
