// Created by Samuel Teixera Parchao
// Last modified: 13/02/2026
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WeatherApp.Services;

internal sealed class WeatherService
{
    private static readonly HttpClient Http = new();

    /// <summary>
    /// Represents the current weather conditions at a specific location, including temperature, weather code, humidity,
    /// wind speed, and UV index.
    /// </summary>
    /// <param name="Temperature">The current air temperature, in degrees Celsius.</param>
    /// <param name="WeatherCode">The code indicating the current weather condition. Refer to the weather service documentation for possible
    /// values.</param>
    /// <param name="Humidity">The current relative humidity, as a percentage value between 0 and 100.</param>
    /// <param name="WindSpeed">The current wind speed, in meters per second.</param>
    /// <param name="WindDirection">The current wind direction, in degrees.</param>
    /// <param name="UVIndex">The current ultraviolet (UV) index, indicating the level of UV radiation.</param>
    public record CurrentWeather(
        double Temperature, int WeatherCode, double Humidity,
        double WindSpeed, double WindDirection, double UVIndex);

    /// <summary>
    /// Represents the daily weather forecast for a specific date, including temperature, humidity, wind speed, and UV
    /// index information.
    /// </summary>
    /// <param name="Date">The date for which the forecast applies.</param>
    /// <param name="WeatherCode">The code indicating the general weather condition for the day. The value corresponds to a predefined set of
    /// weather types.</param>
    /// <param name="TempMax">The maximum temperature, in degrees Celsius, expected for the day.</param>
    /// <param name="TempMin">The minimum temperature, in degrees Celsius, expected for the day.</param>
    /// <param name="Humidity">The average relative humidity, as a percentage, forecasted for the day.</param>
    /// <param name="WindSpeed">The average wind speed, in meters per second, forecasted for the day.</param>
    /// <param name="WindDirection">The dominant wind direction for the day, in degrees.</param>
    /// <param name="UVIndex">The maximum UV index expected for the day. Higher values indicate greater risk from sun exposure.</param>
    public record DailyForecast(
        DateTime Date, int WeatherCode, double TempMax, double TempMin,
        double Humidity, double WindSpeed, double WindDirection, double UVIndex);

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
            $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m,wind_direction_10m,uv_index&daily=weather_code,temperature_2m_max,temperature_2m_min,relative_humidity_2m_max,wind_speed_10m_max,wind_direction_10m_dominant,uv_index_max&timezone=auto&forecast_days=7");

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
            cur.GetProperty("wind_direction_10m").GetDouble(),
            cur.GetProperty("uv_index").GetDouble());

        var daily = root.GetProperty("daily");
        var times = daily.GetProperty("time");
        var codes = daily.GetProperty("weather_code");
        var maxT = daily.GetProperty("temperature_2m_max");
        var minT = daily.GetProperty("temperature_2m_min");
        var hum = daily.GetProperty("relative_humidity_2m_max");
        var wind = daily.GetProperty("wind_speed_10m_max");
        var windDir = daily.GetProperty("wind_direction_10m_dominant");
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
                windDir[i].GetDouble(),
                uv[i].GetDouble());
        }

        return new WeatherData(current, forecasts);
    }

    /// <summary>
    /// Maps a weather condition code to its corresponding description, icon, and representative color.
    /// </summary>
    /// <remarks>The returned icon uses Unicode glyphs commonly found in weather font sets. The RGB values can
    /// be used for UI theming or visualization. If the code is not recognized, the method returns a generic "Unknown"
    /// description and icon.</remarks>
    /// <param name="code">The weather condition code to map. Valid codes correspond to standard meteorological values; values outside the
    /// defined set will return "Unknown".</param>
    /// <returns>A tuple containing the weather description, icon Unicode string, and RGB color components representing the
    /// condition.</returns>
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
