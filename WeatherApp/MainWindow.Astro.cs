// Created by Samuel Teixeira Parchao
// Last modified: 14/02/2026
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WeatherApp.Services;
using Windows.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace WeatherApp
{
    public sealed partial class MainWindow
    {
        private Grid CreateAstroPage()
        {
            Grid astroGrid = new Grid
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0.3, 0),
                    EndPoint = new Windows.Foundation.Point(0.7, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x08, 0x11, 0x24), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x12, 0x1E, 0x3A), Offset = 0.35 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x1D, 0x2E, 0x52), Offset = 0.72 },
                        new GradientStop { Color = Color.FromArgb(0xFF, 0x2A, 0x3E, 0x68), Offset = 1 }
                    }
                }
            };
            astroGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            astroGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel headerStack = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(12, 46, 12, 10)
            };

            Grid topHeaderRow = new Grid();
            topHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topHeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button closeAstroButton = new Button
            {
                Content = new SymbolIcon(Symbol.Back),
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            closeAstroButton.Click += OnCloseAstroPageClick;
            Grid.SetColumn(closeAstroButton, 0);
            topHeaderRow.Children.Add(closeAstroButton);

            TextBlock sectionLabel = new TextBlock
            {
                Text = "ASTRO TARGETS",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)),
                CharacterSpacing = 80,
                FontFamily = TextFont,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(sectionLabel, 1);
            topHeaderRow.Children.Add(sectionLabel);

            headerStack.Children.Add(topHeaderRow);

            astroPageTitle = new TextBlock
            {
                Text = "Best targets for tonight",
                FontSize = 20,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = DisplayFont,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            headerStack.Children.Add(astroPageTitle);

            Grid.SetRow(headerStack, 0);
            astroGrid.Children.Add(headerStack);

            astroTargetsStack = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 10)
            };

            ScrollViewer targetsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(12, 0, 12, 0),
                Content = astroTargetsStack
            };
            Grid.SetRow(targetsScroll, 1);
            astroGrid.Children.Add(targetsScroll);

            return astroGrid;
        }

        private void RefreshAstroTargetsList()
        {
            if (astroTargetsStack == null)
            {
                return;
            }

            astroTargetsStack.Children.Clear();

            if (astroTargetsLoading)
            {
                astroTargetsStack.Children.Add(new TextBlock
                {
                    Text = "Loading astro targets...",
                    Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    FontSize = 13,
                    Margin = new Thickness(8, 10, 8, 0)
                });

                selectedAstroTargetIndex = -1;
                return;
            }

            if (astroTargets.Count == 0)
            {
                astroTargetsStack.Children.Add(new TextBlock
                {
                    Text = "No targets available for this location and date.",
                    Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    FontSize = 13,
                    Margin = new Thickness(8, 10, 8, 0)
                });

                selectedAstroTargetIndex = -1;
                return;
            }

            if (selectedAstroTargetIndex < 0 || selectedAstroTargetIndex >= astroTargets.Count)
            {
                selectedAstroTargetIndex = 0;
            }

            for (Int32 i = 0; i < astroTargets.Count; i++)
            {
                astroTargetsStack.Children.Add(CreateAstroTargetRow(new AstroTargetListItem(i, astroTargets[i])));
            }
        }

        private Button CreateAstroTargetRow(AstroTargetListItem item)
        {
            Boolean isSelected = item.Index == selectedAstroTargetIndex;
            Boolean isExpanded = isSelected && astroExpanded;

            StackPanel rowContent = new StackPanel
            {
                Spacing = 8
            };

            Grid headerGrid = new Grid
            {
                MinHeight = 54
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });

            StackPanel left = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            left.Children.Add(new TextBlock
            {
                Text = item.Target.Name,
                FontSize = 15,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = DisplayFont,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            left.Children.Add(new TextBlock
            {
                Text = item.Target.Category,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0x8A, 0xFF, 0xFF, 0xFF)),
                FontFamily = TextFont,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(left, 0);
            headerGrid.Children.Add(left);

            TextBlock right = new TextBlock
            {
                Text = $"{item.Target.VisibilityScore:F0}%",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xD4, 0x5E)),
                FontFamily = DisplayFont,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(right, 1);
            headerGrid.Children.Add(right);
            rowContent.Children.Add(headerGrid);

            if (isExpanded)
            {
                Border divider = new Border
                {
                    Height = 0.5,
                    Margin = new Thickness(0, -2, 0, 2),
                    Background = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF))
                };
                rowContent.Children.Add(divider);

                rowContent.Children.Add(new TextBlock
                {
                    Text = item.Target.Summary,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    TextWrapping = TextWrapping.Wrap
                });

                String telescopeDirection = GetWindDirection(item.Target.AzimuthDegrees);
                rowContent.Children.Add(new TextBlock
                {
                    Text = $"Alt {item.Target.AltitudeDegrees:F0}°  •  Az {item.Target.AzimuthDegrees:F0}° ({telescopeDirection})",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontFamily = DisplayFont,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });

                rowContent.Children.Add(new TextBlock
                {
                    Text = $"Best window: {item.Target.BestTimeWindow}   •   Mag {item.Target.Magnitude:F1}",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    TextWrapping = TextWrapping.Wrap
                });

                rowContent.Children.Add(new TextBlock
                {
                    Text = item.Target.ExpandableDetails,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x9A, 0xFF, 0xFF, 0xFF)),
                    FontFamily = TextFont,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            Button rowButton = new Button
            {
                Tag = item.Index,
                MinHeight = 74,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new AcrylicBrush
                {
                    TintColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
                    TintOpacity = isSelected ? 0.16 : 0.08,
                    FallbackColor = isSelected
                        ? Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)
                        : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)
                },
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = rowContent
            };
            rowButton.Click += OnAstroTargetRowClick;
            return rowButton;
        }

        private void UpdateAstroTargetDetails()
        {
            if (astroSummaryText == null || astroCoordinatesText == null || astroBestWindowText == null || astroExtraText == null || astroExpandButton == null)
            {
                return;
            }

            if (selectedAstroTargetIndex < 0 || selectedAstroTargetIndex >= astroTargets.Count)
            {
                astroSummaryText.Text = "Select a target to see details.";
                astroCoordinatesText.Text = string.Empty;
                astroBestWindowText.Text = string.Empty;
                astroExtraText.Text = string.Empty;
                astroExtraText.Visibility = Visibility.Collapsed;
                astroExpandButton.Visibility = Visibility.Collapsed;
                return;
            }

            AstroService.AstroTargetInfo target = astroTargets[selectedAstroTargetIndex];
            astroSummaryText.Text = target.Summary;
            astroSummaryText.MaxLines = astroExpanded ? 0 : 4;
            astroSummaryText.TextTrimming = astroExpanded ? TextTrimming.None : TextTrimming.WordEllipsis;
            astroCoordinatesText.Text = $"Alt {target.AltitudeDegrees:F0}°  •  Az {target.AzimuthDegrees:F0}°";
            astroBestWindowText.Text = $"{target.Category}  •  Mag {target.Magnitude:F1}  •  Best: {target.BestTimeWindow}";
            astroExtraText.Text = target.ExpandableDetails;
            astroExtraText.Visibility = astroExpanded ? Visibility.Visible : Visibility.Collapsed;
            astroExpandButton.Visibility = Visibility.Visible;
            astroExpandButton.Content = astroExpanded ? "Show less" : "Show more";
        }

        private async void OnOpenAstroTargetsClick(object sender, RoutedEventArgs e)
        {
            await OpenAstroTargetsForSelectedDayAsync();
        }

        private void OnCloseAstroPageClick(object sender, RoutedEventArgs e)
        {
            SwitchToForecast();
        }

        private void SwitchToAstroPage()
        {
            UIElement source = null;
            if (forecastContent.Visibility == Visibility.Visible)
            {
                source = forecastContent;
            }
            else if (mapWebView.Visibility == Visibility.Visible)
            {
                source = mapWebView;
            }
            else if (cityListContent.Visibility == Visibility.Visible)
            {
                source = cityListContent;
            }

            if (source == null)
            {
                source = forecastContent;
            }

            if (source != mapWebView)
            {
                mapWebView.Visibility = Visibility.Collapsed;
            }

            if (source != cityListContent)
            {
                cityListContent.Visibility = Visibility.Collapsed;
            }

            if (source != forecastContent)
            {
                forecastContent.Visibility = Visibility.Collapsed;
            }

            astroContent.Visibility = Visibility.Visible;
            astroContent.Opacity = 0;
            astroContent.RenderTransform = new TranslateTransform { Y = 20 };

            if (source != astroContent)
            {
                source.Visibility = Visibility.Visible;
                source.Opacity = 1;
                source.RenderTransform = new TranslateTransform { Y = 0 };
            }

            RefreshCityNavigator();

            Storyboard storyboard = new Storyboard();

            if (source != astroContent)
            {
                DoubleAnimation sourceFade = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(230)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(sourceFade, source);
                Storyboard.SetTargetProperty(sourceFade, "Opacity");
                storyboard.Children.Add(sourceFade);

                DoubleAnimation sourceSlide = new DoubleAnimation
                {
                    From = 0,
                    To = -8,
                    Duration = new Duration(TimeSpan.FromMilliseconds(230)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(sourceSlide, source.RenderTransform);
                Storyboard.SetTargetProperty(sourceSlide, "Y");
                storyboard.Children.Add(sourceSlide);
            }

            DoubleAnimation targetFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(targetFade, astroContent);
            Storyboard.SetTargetProperty(targetFade, "Opacity");
            storyboard.Children.Add(targetFade);

            if (astroContent.RenderTransform is TranslateTransform astroTranslate)
            {
                DoubleAnimation targetSlide = new DoubleAnimation
                {
                    From = 20,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(targetSlide, astroTranslate);
                Storyboard.SetTargetProperty(targetSlide, "Y");
                storyboard.Children.Add(targetSlide);
            }

            storyboard.Completed += (s, e) =>
            {
                if (source != astroContent)
                {
                    source.Visibility = Visibility.Collapsed;
                    source.Opacity = 1;
                    if (source.RenderTransform is TranslateTransform sourceTranslate)
                    {
                        sourceTranslate.Y = 0;
                    }
                }
            };

            storyboard.Begin();
        }

        private async Task OpenAstroTargetsForSelectedDayAsync()
        {
            if (selectedDayIndex == null || currentWeather == null || trackedCities.Count == 0)
            {
                return;
            }

            Int32 dayIndex = Math.Clamp(selectedDayIndex.Value, 0, currentWeather.Daily.Length - 1);
            DateTime forecastDate = currentWeather.Daily[dayIndex].Date;
            TrackedCity activeCity = trackedCities[activeTrackedCityIndex];

            astroForecastDate = forecastDate;
            selectedAstroTargetIndex = -1;
            astroExpanded = false;
            astroTargets.Clear();
            astroTargetsLoading = true;

            if (astroPageTitle != null)
            {
                astroPageTitle.Text = string.Create(CultureInfo.InvariantCulture, $"{activeCity.Name} • {forecastDate:ddd, MMM d}");
            }

            HideDetails();
            SwitchToAstroPage();
            RefreshAstroTargetsList();

            try
            {
                IReadOnlyList<AstroService.AstroTargetInfo> targets = await AstroService.GetTopTargetsAsync(
                    activeCity.Latitude,
                    activeCity.Longitude,
                    forecastDate,
                    5);

                astroTargets.Clear();
                foreach (AstroService.AstroTargetInfo target in targets)
                {
                    astroTargets.Add(target);
                }
            }
            catch
            {
                astroTargets.Clear();
            }
            finally
            {
                astroTargetsLoading = false;
            }

            selectedAstroTargetIndex = astroTargets.Count > 0 ? 0 : -1;
            astroExpanded = false;
            RefreshAstroTargetsList();
        }

        private void OnAstroTargetRowClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button targetButton || targetButton.Tag is not Int32 targetIndex)
            {
                return;
            }

            if (selectedAstroTargetIndex == targetIndex)
            {
                astroExpanded = !astroExpanded;
            }
            else
            {
                selectedAstroTargetIndex = targetIndex;
                astroExpanded = true;
            }

            RefreshAstroTargetsList();
        }

        private void OnAstroExpandButtonClick(object sender, RoutedEventArgs e)
        {
            astroExpanded = !astroExpanded;
            UpdateAstroTargetDetails();
        }
    }
}
