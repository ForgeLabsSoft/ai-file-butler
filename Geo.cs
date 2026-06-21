namespace AIFileButler;

/// <summary>Offline reverse-geocoding: maps GPS coordinates to the nearest
/// well-known city, so photos can be filed under a place name without internet.
/// The list is a curated sample of major world cities (Romania-heavy).</summary>
internal static class Geo
{
    private static readonly (string City, double Lat, double Lon)[] Cities =
    {
        // Romania
        ("Bucuresti", 44.43, 26.10), ("Cluj-Napoca", 46.77, 23.60), ("Timisoara", 45.76, 21.23),
        ("Iasi", 47.16, 27.59), ("Constanta", 44.18, 28.63), ("Brasov", 45.66, 25.61),
        ("Craiova", 44.32, 23.80), ("Oradea", 47.07, 21.92), ("Sibiu", 45.79, 24.15),
        ("Ploiesti", 44.94, 26.03), ("Galati", 45.44, 28.05),
        // Europe
        ("London", 51.51, -0.13), ("Paris", 48.86, 2.35), ("Berlin", 52.52, 13.40),
        ("Madrid", 40.42, -3.70), ("Barcelona", 41.39, 2.17), ("Rome", 41.90, 12.50),
        ("Milan", 45.46, 9.19), ("Vienna", 48.21, 16.37), ("Amsterdam", 52.37, 4.90),
        ("Brussels", 50.85, 4.35), ("Munich", 48.14, 11.58), ("Zurich", 47.37, 8.54),
        ("Prague", 50.08, 14.44), ("Warsaw", 52.23, 21.01), ("Budapest", 47.50, 19.04),
        ("Athens", 37.98, 23.73), ("Lisbon", 38.72, -9.14), ("Dublin", 53.35, -6.26),
        ("Stockholm", 59.33, 18.07), ("Oslo", 59.91, 10.75), ("Copenhagen", 55.68, 12.57),
        ("Helsinki", 60.17, 24.94), ("Moscow", 55.75, 37.62), ("Kyiv", 50.45, 30.52),
        ("Istanbul", 41.01, 28.98), ("Sofia", 42.70, 23.32), ("Belgrade", 44.79, 20.45),
        ("Zagreb", 45.81, 15.98), ("Manchester", 53.48, -2.24),
        // Americas
        ("New York", 40.71, -74.01), ("Los Angeles", 34.05, -118.24), ("Chicago", 41.88, -87.63),
        ("San Francisco", 37.77, -122.42), ("Toronto", 43.65, -79.38), ("Mexico City", 19.43, -99.13),
        ("Miami", 25.76, -80.19), ("Seattle", 47.61, -122.33), ("Boston", 42.36, -71.06),
        ("Washington", 38.91, -77.04), ("Sao Paulo", -23.55, -46.63), ("Rio de Janeiro", -22.91, -43.17),
        ("Buenos Aires", -34.60, -58.38), ("Lima", -12.05, -77.04), ("Bogota", 4.71, -74.07),
        // Asia / Middle East
        ("Tokyo", 35.68, 139.69), ("Osaka", 34.69, 135.50), ("Beijing", 39.90, 116.41),
        ("Shanghai", 31.23, 121.47), ("Hong Kong", 22.32, 114.17), ("Singapore", 1.35, 103.82),
        ("Seoul", 37.57, 126.98), ("Bangkok", 13.76, 100.50), ("Mumbai", 19.08, 72.88),
        ("Delhi", 28.61, 77.21), ("Bangalore", 12.97, 77.59), ("Dubai", 25.20, 55.27),
        ("Tel Aviv", 32.08, 34.78), ("Jakarta", -6.21, 106.85), ("Kuala Lumpur", 3.14, 101.69),
        ("Manila", 14.60, 120.98),
        // Africa
        ("Cairo", 30.04, 31.24), ("Lagos", 6.52, 3.38), ("Johannesburg", -26.20, 28.05),
        ("Cape Town", -33.92, 18.42), ("Nairobi", -1.29, 36.82), ("Casablanca", 33.57, -7.59),
        // Oceania
        ("Sydney", -33.87, 151.21), ("Melbourne", -37.81, 144.96), ("Auckland", -36.85, 174.76),
    };

    /// <summary>Nearest city name to the given coordinates (great-circle-ish).</summary>
    public static string NearestCity(double lat, double lon)
    {
        string best = "";
        double bestDist = double.MaxValue;
        double cosLat = Math.Cos(lat * Math.PI / 180.0);
        foreach (var (city, clat, clon) in Cities)
        {
            double dLat = lat - clat;
            double dLon = (lon - clon) * cosLat; // scale longitude by latitude
            double d = dLat * dLat + dLon * dLon;
            if (d < bestDist) { bestDist = d; best = city; }
        }
        return best;
    }
}
