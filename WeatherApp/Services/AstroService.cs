// Created by Samuel Teixeira Parchao
// Last modified: 14/02/2026
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WeatherApp.Services;

internal sealed class AstroService
{
    private static readonly HttpClient Http = new HttpClient
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "WeatherApp/1.0" }
        }
    };

    private static readonly AstroCatalogItem[] Catalog =
    [
        new AstroCatalogItem("Andromeda Galaxy", "Andromeda_Galaxy", "Galaxy", 0.712, 41.269, 3.4),
        new AstroCatalogItem("Orion Nebula", "Orion_Nebula", "Nebula", 5.588, -5.391, 4.0),
        new AstroCatalogItem("Pleiades", "Pleiades", "Open Cluster", 3.792, 24.117, 1.6),
        new AstroCatalogItem("Lagoon Nebula", "Lagoon_Nebula", "Nebula", 18.060, -24.384, 6.0),
        new AstroCatalogItem("Eagle Nebula", "Eagle_Nebula", "Nebula", 18.314, -13.806, 6.4),
        new AstroCatalogItem("Ring Nebula", "Ring_Nebula", "Planetary Nebula", 18.886, 33.029, 8.8),
        new AstroCatalogItem("Jupiter", "Jupiter", "Planet", 2.000, 12.000, -2.3),
        new AstroCatalogItem("Saturn", "Saturn", "Planet", 22.000, -12.000, 0.6),
        new AstroCatalogItem("Mars", "Mars", "Planet", 7.000, 18.000, 0.8),
        new AstroCatalogItem("Moon", "Moon", "Natural Satellite", 8.000, 22.000, -12.0)
    ];

    private record AstroCatalogItem(
        String Name,
        String SummarySlug,
        String Category,
        Double RightAscensionHours,
        Double DeclinationDegrees,
        Double Magnitude);

    public record AstroTargetInfo(
        String Name,
        String Category,
        String Summary,
        Double AltitudeDegrees,
        Double AzimuthDegrees,
        String BestTimeWindow,
        Double Magnitude,
        Double VisibilityScore,
        String ExpandableDetails);

    public static async Task<IReadOnlyList<AstroTargetInfo>> GetTopTargetsAsync(
        Double latitude,
        Double longitude,
        DateTime localDate,
        Int32 count = 5)
    {
        List<(AstroCatalogItem Item, TargetComputation Computation)> ranked = new List<(AstroCatalogItem, TargetComputation)>();

        foreach (AstroCatalogItem item in Catalog)
        {
            TargetComputation computation = ComputeTarget(latitude, longitude, localDate, item);
            if (computation.VisibilityScore <= 0)
            {
                continue;
            }

            ranked.Add((item, computation));
        }

        ranked.Sort(static (left, right) => right.Computation.VisibilityScore.CompareTo(left.Computation.VisibilityScore));

        Int32 targetCount = Math.Min(Math.Max(1, count), ranked.Count);
        List<AstroTargetInfo> results = new List<AstroTargetInfo>();

        for (Int32 i = 0; i < targetCount; i++)
        {
            AstroCatalogItem item = ranked[i].Item;
            TargetComputation computation = ranked[i].Computation;
            String summary = await GetSummaryAsync(item.Name, item.SummarySlug);

            String fullDetails = string.Create(
                CultureInfo.InvariantCulture,
                $"Magnitude: {item.Magnitude:F1} • Peak visibility score: {computation.VisibilityScore:F0}/100 • Position near best time: Alt {computation.BestAltitude:F0}°, Az {computation.BestAzimuth:F0}°.");

            results.Add(new AstroTargetInfo(
                item.Name,
                item.Category,
                summary,
                computation.BestAltitude,
                computation.BestAzimuth,
                computation.BestWindowLabel,
                item.Magnitude,
                computation.VisibilityScore,
                fullDetails));
        }

        return results;
    }

    private static TargetComputation ComputeTarget(Double latitude, Double longitude, DateTime localDate, AstroCatalogItem item)
    {
        DateTime eveningStart = DateTime.SpecifyKind(new DateTime(localDate.Year, localDate.Month, localDate.Day, 20, 0, 0), DateTimeKind.Local);
        DateTime eveningEnd = eveningStart.AddHours(8);

        Double bestAltitude = Double.MinValue;
        Double bestAzimuth = 0;
        DateTime bestTime = eveningStart;

        DateTime? windowStart = null;
        DateTime? windowEnd = null;

        DateTime cursor = eveningStart;
        while (cursor <= eveningEnd)
        {
            DateTime utcTime = cursor.ToUniversalTime();
            HorizontalCoordinates coordinates = ComputeHorizontalCoordinates(
                latitude,
                longitude,
                item.RightAscensionHours,
                item.DeclinationDegrees,
                utcTime);

            if (coordinates.AltitudeDegrees > bestAltitude)
            {
                bestAltitude = coordinates.AltitudeDegrees;
                bestAzimuth = coordinates.AzimuthDegrees;
                bestTime = cursor;
            }

            if (coordinates.AltitudeDegrees >= 25)
            {
                if (windowStart == null)
                {
                    windowStart = cursor;
                }

                windowEnd = cursor;
            }

            cursor = cursor.AddMinutes(30);
        }

        if (bestAltitude < 10)
        {
            return new TargetComputation(0, bestAltitude, bestAzimuth, "Below practical horizon");
        }

        String windowLabel;
        if (windowStart != null && windowEnd != null)
        {
            windowLabel = $"{windowStart.Value:HH:mm} - {windowEnd.Value:HH:mm}";
        }
        else
        {
            windowLabel = $"Best around {bestTime:HH:mm}";
        }

        Double altitudeScore = Math.Clamp((bestAltitude - 10) * 2.2, 0, 80);
        Double brightnessScore = Math.Clamp((6.5 - item.Magnitude) * 6, 0, 20);
        Double visibilityScore = Math.Clamp(altitudeScore + brightnessScore, 1, 100);

        return new TargetComputation(visibilityScore, bestAltitude, bestAzimuth, windowLabel);
    }

    private static HorizontalCoordinates ComputeHorizontalCoordinates(
        Double latitude,
        Double longitude,
        Double rightAscensionHours,
        Double declinationDegrees,
        DateTime utcTime)
    {
        Double rightAscensionDegrees = rightAscensionHours * 15.0;
        Double julianDate = GetJulianDate(utcTime);
        Double greenwichSiderealTime = 280.46061837 + 360.98564736629 * (julianDate - 2451545.0);
        Double localSiderealDegrees = NormalizeDegrees(greenwichSiderealTime + longitude);
        Double hourAngleDegrees = NormalizeDegrees(localSiderealDegrees - rightAscensionDegrees);

        Double latitudeRadians = DegreesToRadians(latitude);
        Double declinationRadians = DegreesToRadians(declinationDegrees);
        Double hourAngleRadians = DegreesToRadians(hourAngleDegrees);

        Double sinAltitude = Math.Sin(declinationRadians) * Math.Sin(latitudeRadians) +
                             Math.Cos(declinationRadians) * Math.Cos(latitudeRadians) * Math.Cos(hourAngleRadians);
        Double altitudeRadians = Math.Asin(Math.Clamp(sinAltitude, -1, 1));

        Double cosAzimuth = (Math.Sin(declinationRadians) - Math.Sin(altitudeRadians) * Math.Sin(latitudeRadians)) /
                            (Math.Cos(altitudeRadians) * Math.Cos(latitudeRadians));
        Double azimuthRadians = Math.Acos(Math.Clamp(cosAzimuth, -1, 1));
        if (Math.Sin(hourAngleRadians) > 0)
        {
            azimuthRadians = 2 * Math.PI - azimuthRadians;
        }

        return new HorizontalCoordinates(
            RadiansToDegrees(altitudeRadians),
            RadiansToDegrees(azimuthRadians));
    }

    private static Double GetJulianDate(DateTime utcTime)
    {
        DateTime reference = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeSpan elapsed = utcTime - reference;
        return 2451545.0 + elapsed.TotalDays;
    }

    private static Double DegreesToRadians(Double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static Double RadiansToDegrees(Double radians)
    {
        return radians * 180.0 / Math.PI;
    }

    private static Double NormalizeDegrees(Double angle)
    {
        Double normalized = angle % 360.0;
        if (normalized < 0)
        {
            normalized += 360.0;
        }

        return normalized;
    }

    private static async Task<String> GetSummaryAsync(String objectName, String summarySlug)
    {
        try
        {
            String encodedSlug = Uri.EscapeDataString(summarySlug);
            String url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{encodedSlug}";

            using HttpResponseMessage response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return $"{objectName} is a strong target tonight with favorable placement for imaging.";
            }

            using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("extract", out JsonElement extractElement))
            {
                String? extract = extractElement.GetString();
                if (!string.IsNullOrWhiteSpace(extract))
                {
                    return extract;
                }
            }
        }
        catch
        {
        }

        return $"{objectName} is a strong target tonight with favorable placement for imaging.";
    }

    private record TargetComputation(Double VisibilityScore, Double BestAltitude, Double BestAzimuth, String BestWindowLabel);

    private record HorizontalCoordinates(Double AltitudeDegrees, Double AzimuthDegrees);
}
