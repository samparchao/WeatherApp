// Created by Samuel Teixeira Parchao
// Last modified: 13/02/2026
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace WeatherApp.Services;

internal sealed class LocationService
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "WeatherApp/1.0" } }
    };

    /// <summary>
    /// Represents the result of a location query, including the city name and geographic coordinates.
    /// </summary>
    /// <param name="CityName">The name of the city associated with the location. Cannot be null or empty.</param>
    /// <param name="Latitude">The latitude component of the location, in decimal degrees. Valid values range from -90 to 90.</param>
    /// <param name="Longitude">The longitude component of the location, in decimal degrees. Valid values range from -180 to 180.</param>
    public record LocationResult(string CityName, double Latitude, double Longitude);

    /// <summary>
    /// Represents a city search result item with display text and location coordinates.
    /// </summary>
    /// <param name="CityName">Short city name shown in compact UI contexts.</param>
    /// <param name="DisplayName">Expanded location label including country or region.</param>
    /// <param name="Latitude">Latitude in decimal degrees.</param>
    /// <param name="Longitude">Longitude in decimal degrees.</param>
    public record CitySearchResult(string CityName, string DisplayName, double Latitude, double Longitude)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    /// <summary>
    /// Asynchronously retrieves the device's current geographic location, including latitude, longitude, and the
    /// associated city name.
    /// </summary>
    /// <remarks>This method requests permission to access location data. If permission is not granted, the
    /// returned <see cref="LocationResult"/> will indicate the denial and provide default values. The method may take
    /// several seconds to complete, depending on device settings and network conditions.</remarks>
    /// <returns>A <see cref="LocationResult"/> containing the city name, latitude, and longitude of the current location. If
    /// location access is denied, the result contains default coordinates and an error message.</returns>
    public static async Task<LocationResult> GetCurrentLocationAsync()
    {
        GeolocationAccessStatus status = await Geolocator.RequestAccessAsync();
        if (status != GeolocationAccessStatus.Allowed)
            return new LocationResult("Location denied", 0, 0);

        Geolocator geolocator = new Geolocator { DesiredAccuracyInMeters = 1000 };
        Geoposition position = await geolocator.GetGeopositionAsync();
        Double lat = position.Coordinate.Point.Position.Latitude;
        Double lon = position.Coordinate.Point.Position.Longitude;

        String cityName = await ReverseGeocodeAsync(lat, lon);
        return new LocationResult(cityName, lat, lon);
    }

    public static async Task<IReadOnlyList<CitySearchResult>> SearchCitiesAsync(string query, int limit = 6)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<CitySearchResult>();
        }

        String escapedQuery = Uri.EscapeDataString(query.Trim());
        Int32 safeLimit = Math.Clamp(limit, 1, 10);
        String url = $"https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&accept-language=en&limit={safeLimit}&q={escapedQuery}";

        using HttpResponseMessage response = await Http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CitySearchResult>();
        }

        List<CitySearchResult> cities = new List<CitySearchResult>();
        foreach (JsonElement element in root.EnumerateArray())
        {
            if (!element.TryGetProperty("lat", out JsonElement latElement) ||
                !element.TryGetProperty("lon", out JsonElement lonElement))
            {
                continue;
            }

            if (!Double.TryParse(latElement.GetString(), out Double latitude) ||
                !Double.TryParse(lonElement.GetString(), out Double longitude))
            {
                continue;
            }

            String displayName = element.TryGetProperty("display_name", out JsonElement displayElement)
                ? displayElement.GetString() ?? "Unknown location"
                : "Unknown location";

            String cityName = BuildCityLabel(element, displayName);
            cities.Add(new CitySearchResult(cityName, displayName, latitude, longitude));
        }

        return cities;
    }

    private static String BuildCityLabel(JsonElement element, String fallback)
    {
        if (element.TryGetProperty("address", out JsonElement address))
        {
            if (address.TryGetProperty("city", out JsonElement city))
            {
                return city.GetString() ?? fallback;
            }

            if (address.TryGetProperty("town", out JsonElement town))
            {
                return town.GetString() ?? fallback;
            }

            if (address.TryGetProperty("village", out JsonElement village))
            {
                return village.GetString() ?? fallback;
            }

            if (address.TryGetProperty("municipality", out JsonElement municipality))
            {
                return municipality.GetString() ?? fallback;
            }

            if (address.TryGetProperty("state", out JsonElement state))
            {
                return state.GetString() ?? fallback;
            }
        }

        return fallback.Split(',')[0];
    }

    private static async Task<string> ReverseGeocodeAsync(double lat, double lon)
    {
        try
        {
            String url = FormattableString.Invariant(
                $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json&accept-language=en");

            using HttpResponseMessage response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

            JsonElement address = doc.RootElement.GetProperty("address");

            if (address.TryGetProperty("city", out JsonElement city))
                return city.GetString()!;
            if (address.TryGetProperty("town", out JsonElement town))
                return town.GetString()!;
            if (address.TryGetProperty("village", out JsonElement village))
                return village.GetString()!;
            if (address.TryGetProperty("county", out JsonElement county))
                return county.GetString()!;
            if (address.TryGetProperty("state", out JsonElement state))
                return state.GetString()!;

            return doc.RootElement.GetProperty("display_name")
                .GetString()!.Split(',')[0];
        }
        catch
        {
            return "Unknown Location";
        }
    }
}
