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

    private static readonly String[] CatalogEndpoints =
    [
        "https://raw.githubusercontent.com/ofrohn/d3-celestial/master/data/messier.json",
        "https://raw.githubusercontent.com/ofrohn/d3-celestial/master/data/dso.en.json"
    ];

    private static List<AstroCatalogItem> dynamicCatalogCache = new List<AstroCatalogItem>();
    private static DateTime cacheExpiresUtc = DateTime.MinValue;

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
        IReadOnlyList<AstroCatalogItem> catalog = await GetDynamicCatalogAsync();
        if (catalog.Count == 0)
        {
            return Array.Empty<AstroTargetInfo>();
        }

        List<(AstroCatalogItem Item, TargetComputation Computation)> ranked = new List<(AstroCatalogItem, TargetComputation)>();

        foreach (AstroCatalogItem item in catalog)
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

    private static async Task<IReadOnlyList<AstroCatalogItem>> GetDynamicCatalogAsync()
    {
        DateTime utcNow = DateTime.UtcNow;
        if (dynamicCatalogCache.Count > 0 && utcNow < cacheExpiresUtc)
        {
            return dynamicCatalogCache;
        }

        List<AstroCatalogItem> loadedCatalog = await DownloadCatalogAsync();
        if (loadedCatalog.Count > 0)
        {
            dynamicCatalogCache = loadedCatalog;
            cacheExpiresUtc = utcNow.AddHours(12);
            return dynamicCatalogCache;
        }

        return dynamicCatalogCache;
    }

    private static async Task<List<AstroCatalogItem>> DownloadCatalogAsync()
    {
        List<AstroCatalogItem> items = new List<AstroCatalogItem>();

        foreach (String endpoint in CatalogEndpoints)
        {
            try
            {
                using HttpResponseMessage response = await Http.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                ParseCatalogPayload(document.RootElement, items);

                if (items.Count >= 120)
                {
                    break;
                }
            }
            catch
            {
            }
        }

        return items;
    }

    private static void ParseCatalogPayload(JsonElement root, List<AstroCatalogItem> items)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in root.EnumerateArray())
            {
                if (TryParseCatalogItem(element, out AstroCatalogItem parsedItem))
                {
                    items.Add(parsedItem);
                }
            }

            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (root.TryGetProperty("features", out JsonElement features) && features.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement feature in features.EnumerateArray())
            {
                if (TryParseCatalogItem(feature, out AstroCatalogItem parsedItem))
                {
                    items.Add(parsedItem);
                }
            }
        }
    }

    private static Boolean TryParseCatalogItem(JsonElement element, out AstroCatalogItem item)
    {
        item = null;

        JsonElement source = element;
        JsonElement properties = default;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("properties", out JsonElement propertiesNode))
        {
            properties = propertiesNode;
            source = propertiesNode;
        }

        String name = GetFirstString(source, ["name", "desig", "id", "designation"]);
        if (String.IsNullOrWhiteSpace(name) && properties.ValueKind == JsonValueKind.Object)
        {
            name = GetFirstString(properties, ["name", "desig", "id", "designation"]);
        }

        if (String.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        String category = GetFirstString(source, ["type", "category", "class"]);
        if (String.IsNullOrWhiteSpace(category))
        {
            category = "Deep Sky";
        }

        Double magnitude;
        if (!TryGetFirstDouble(source, ["mag", "magnitude", "vmag"], out magnitude))
        {
            magnitude = 6.5;
        }

        if (magnitude > 10.0)
        {
            return false;
        }

        Double rightAscensionHours;
        Double declinationDegrees;

        if (!TryGetFirstDouble(source, ["ra", "rightAscension"], out Double raRaw) ||
            !TryGetFirstDouble(source, ["dec", "declination"], out Double decRaw))
        {
            if (!TryParseCoordinatesFromGeometry(element, out raRaw, out decRaw))
            {
                return false;
            }
        }

        if (Math.Abs(raRaw) > 24)
        {
            rightAscensionHours = NormalizeDegrees(raRaw) / 15.0;
        }
        else
        {
            rightAscensionHours = NormalizeHours(raRaw);
        }

        declinationDegrees = decRaw;
        if (declinationDegrees is < -90 or > 90)
        {
            return false;
        }

        String summarySlug = BuildSummarySlug(name);
        item = new AstroCatalogItem(name, summarySlug, category, rightAscensionHours, declinationDegrees, magnitude);
        return true;
    }

    private static Boolean TryParseCoordinatesFromGeometry(JsonElement element, out Double rightAscensionDegrees, out Double declinationDegrees)
    {
        rightAscensionDegrees = 0;
        declinationDegrees = 0;

        if (!element.TryGetProperty("geometry", out JsonElement geometry) || geometry.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!geometry.TryGetProperty("coordinates", out JsonElement coordinates) || coordinates.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        JsonElement.ArrayEnumerator enumerator = coordinates.EnumerateArray();
        if (!enumerator.MoveNext())
        {
            return false;
        }

        JsonElement raElement = enumerator.Current;
        if (!TryGetDouble(raElement, out rightAscensionDegrees))
        {
            return false;
        }

        if (!enumerator.MoveNext())
        {
            return false;
        }

        JsonElement decElement = enumerator.Current;
        if (!TryGetDouble(decElement, out declinationDegrees))
        {
            return false;
        }

        return true;
    }

    private static String GetFirstString(JsonElement element, String[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (String name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                String value = property.GetString() ?? string.Empty;
                if (!String.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static Boolean TryGetFirstDouble(JsonElement element, String[] names, out Double value)
    {
        value = 0;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (String name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement property))
            {
                continue;
            }

            if (TryGetDouble(property, out Double parsedValue))
            {
                value = parsedValue;
                return true;
            }
        }

        return false;
    }

    private static Boolean TryGetDouble(JsonElement element, out Double value)
    {
        value = 0;

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            String raw = element.GetString() ?? string.Empty;
            return Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }

    private static String BuildSummarySlug(String name)
    {
        String trimmed = name.Trim();
        if (String.IsNullOrWhiteSpace(trimmed))
        {
            return "Astronomy";
        }

        String normalized = trimmed.Replace(' ', '_');
        return normalized;
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

    private static Double NormalizeHours(Double hours)
    {
        Double normalized = hours % 24.0;
        if (normalized < 0)
        {
            normalized += 24.0;
        }

        return normalized;
    }

    private static async Task<String> GetSummaryAsync(String objectName, String summarySlug)
    {
        HashSet<String> slugCandidates = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            summarySlug,
            $"{summarySlug}_(astronomy)",
            BuildSummarySlug(objectName)
        };

        if (TryExtractMessierNumber(objectName, out Int32 messierNumber))
        {
            slugCandidates.Add($"Messier_{messierNumber}");
            slugCandidates.Add($"M{messierNumber}_(astronomy)");
        }

        if (TryExtractCatalogNumber(objectName, "NGC", out Int32 ngcNumber))
        {
            slugCandidates.Add($"NGC_{ngcNumber}");
        }

        foreach (String slug in slugCandidates)
        {
            String summary = await TryGetWikipediaSummaryAsync(slug);
            if (!String.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }
        }

        IReadOnlyList<String> searchTitles = await SearchAstronomyArticleTitlesAsync(objectName);
        foreach (String title in searchTitles)
        {
            String searchedSummary = await TryGetWikipediaSummaryAsync(title);
            if (!String.IsNullOrWhiteSpace(searchedSummary))
            {
                return searchedSummary;
            }
        }

        return $"{objectName} is a strong target tonight with favorable placement for imaging.";
    }

    private static async Task<String> TryGetWikipediaSummaryAsync(String slug)
    {
        if (String.IsNullOrWhiteSpace(slug))
        {
            return string.Empty;
        }

        try
        {
            String encodedSlug = Uri.EscapeDataString(slug);
            String url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{encodedSlug}";

            using HttpResponseMessage response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("extract", out JsonElement extractElement))
            {
                return string.Empty;
            }

            String extract = extractElement.GetString() ?? string.Empty;
            if (String.IsNullOrWhiteSpace(extract) || IsDisambiguationSummary(extract))
            {
                return string.Empty;
            }

            return extract;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Boolean IsDisambiguationSummary(String summary)
    {
        String normalized = summary.Trim().ToLowerInvariant();
        return normalized.Contains(" refers to:") ||
               normalized.Contains(" may refer to:") ||
               normalized.Contains(" can refer to:");
    }

    private static async Task<IReadOnlyList<String>> SearchAstronomyArticleTitlesAsync(String objectName)
    {
        try
        {
            String searchQuery = Uri.EscapeDataString($"{objectName} astronomy deep sky object");
            String searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&format=json&srlimit=5&srsearch={searchQuery}";

            using HttpResponseMessage response = await Http.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<String>();
            }

            using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (!doc.RootElement.TryGetProperty("query", out JsonElement queryElement) ||
                !queryElement.TryGetProperty("search", out JsonElement searchElement) ||
                searchElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<String>();
            }

            List<String> titles = new List<String>();
            foreach (JsonElement result in searchElement.EnumerateArray())
            {
                if (!result.TryGetProperty("title", out JsonElement titleElement))
                {
                    continue;
                }

                String title = titleElement.GetString() ?? string.Empty;
                if (String.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                String normalizedTitle = title.ToLowerInvariant();
                if (normalizedTitle.Contains("disambiguation") || normalizedTitle.Contains("(surname)") || normalizedTitle.Contains("(given name)"))
                {
                    continue;
                }

                titles.Add(title);
            }

            return titles;
        }
        catch
        {
            return Array.Empty<String>();
        }
    }

    private static Boolean TryExtractMessierNumber(String objectName, out Int32 number)
    {
        number = 0;
        String trimmed = objectName.Trim().ToUpperInvariant();
        if (!trimmed.StartsWith("M", StringComparison.Ordinal))
        {
            return false;
        }

        String suffix = trimmed.Substring(1).Trim();
        return Int32.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    private static Boolean TryExtractCatalogNumber(String objectName, String catalogPrefix, out Int32 number)
    {
        number = 0;
        String trimmed = objectName.Trim().ToUpperInvariant();
        if (!trimmed.StartsWith(catalogPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        String suffix = trimmed.Substring(catalogPrefix.Length).Trim();
        return Int32.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    private record TargetComputation(Double VisibilityScore, Double BestAltitude, Double BestAzimuth, String BestWindowLabel);

    private record HorizontalCoordinates(Double AltitudeDegrees, Double AzimuthDegrees);
}
