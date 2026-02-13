using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WeatherApp;

internal sealed class WeatherService
{
    private static readonly HttpClient Http = new();

    public record CurrentWeather(
        double Temperature, int WeatherCode, double Humidity,
        double WindSpeed, double UVIndex);

    public record DailyForecast(
        DateTime Date, int WeatherCode, double TempMax, double TempMin,
        double Humidity, double WindSpeed, double UVIndex);

    public record WeatherData(CurrentWeather Current, DailyForecast[] Daily);

    /// <summary>
    /// Asynchronously retrieves current and 7-day weather forecast data for the specified geographic coordinates.
    /// </summary>
    /// <remarks>This method uses the Open-Meteo API to obtain weather information, including temperature,
    /// humidity, weather code, wind speed, and UV index. The returned data includes both current conditions and a 7-day
    /// forecast. The method throws an exception if the HTTP request fails or if the response cannot be
    /// parsed.</remarks>
    /// <param name="latitude">The latitude of the location for which to retrieve weather data, in decimal degrees. Valid values are between
    /// -90 and 90.</param>
    /// <param name="longitude">The longitude of the location for which to retrieve weather data, in decimal degrees. Valid values are between
    /// -180 and 180.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a WeatherData object with current
    /// conditions and daily forecasts for the specified location.</returns>
    public static async Task<WeatherData> GetWeatherAsync(double latitude, double longitude)
    {
        var url = FormattableString.Invariant(
            $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m,uv_index&daily=weather_code,temperature_2m_max,temperature_2m_min,relative_humidity_2m_max,wind_speed_10m_max,uv_index_max&timezone=auto&forecast_days=7");

        using var response = await Http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        var root = doc.RootElement;

        var cur = root.GetProperty("current");
        var current = new CurrentWeather(
            cur.GetProperty("temperature_2m").GetDouble(),
            cur.GetProperty("weather_code").GetInt32(),
            cur.GetProperty("relative_humidity_2m").GetDouble(),
            cur.GetProperty("wind_speed_10m").GetDouble(),
            cur.GetProperty("uv_index").GetDouble());

        var daily = root.GetProperty("daily");
        var times = daily.GetProperty("time");
        var codes = daily.GetProperty("weather_code");
        var maxT = daily.GetProperty("temperature_2m_max");
        var minT = daily.GetProperty("temperature_2m_min");
        var hum = daily.GetProperty("relative_humidity_2m_max");
        var wind = daily.GetProperty("wind_speed_10m_max");
        var uv = daily.GetProperty("uv_index_max");

        var count = times.GetArrayLength();
        var forecasts = new DailyForecast[count];
        for (int i = 0; i < count; i++)
        {
            forecasts[i] = new DailyForecast(
                DateTime.Parse(times[i].GetString()!, CultureInfo.InvariantCulture),
                codes[i].GetInt32(),
                maxT[i].GetDouble(),
                minT[i].GetDouble(),
                hum[i].GetDouble(),
                wind[i].GetDouble(),
                uv[i].GetDouble());
        }

        return new WeatherData(current, forecasts);
    }

    public static (string Desc, string Icon, byte R, byte G, byte B) MapWeatherCode(int code) => code switch
    {
        0 => ("Clear Sky", "\uF00D", 0xFF, 0xD4, 0x5E),
        1 => ("Mainly Clear", "\uF00D", 0xFF, 0xD4, 0x5E),
        2 => ("Partly Cloudy", "\uF002", 0xF0, 0xC8, 0x5E),
        3 => ("Overcast", "\uF002", 0xB0, 0xBE, 0xCE),
        45 or 48 => ("Fog", "\uF014", 0xB0, 0xBE, 0xCE),
        51 or 53 or 55 => ("Drizzle", "\uF009", 0x5B, 0xC0, 0xF8),
        56 or 57 => ("Freezing Drizzle", "\uF009", 0x74, 0xAD, 0xD1),
        61 => ("Light Rain", "\uF008", 0x5B, 0xC0, 0xF8),
        63 => ("Rain", "\uF008", 0x5B, 0xC0, 0xF8),
        65 => ("Heavy Rain", "\uF008", 0x5B, 0xC0, 0xF8),
        66 or 67 => ("Freezing Rain", "\uF008", 0x74, 0xAD, 0xD1),
        71 or 73 or 75 or 77 => ("Snow", "\uF01B", 0xE0, 0xF3, 0xF8),
        80 or 81 or 82 => ("Showers", "\uF009", 0x5B, 0xC0, 0xF8),
        85 or 86 => ("Snow Showers", "\uF01B", 0xE0, 0xF3, 0xF8),
        95 => ("Thunderstorm", "\uF010", 0xF0, 0x78, 0x78),
        96 or 99 => ("Thunderstorm + Hail", "\uF010", 0xF0, 0x78, 0x78),
        _ => ("Unknown", "\uF002", 0xB0, 0xBE, 0xCE),
    };
}
