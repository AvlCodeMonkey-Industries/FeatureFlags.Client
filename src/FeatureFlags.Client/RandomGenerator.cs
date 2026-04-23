namespace FeatureFlags.Client;

/// <summary>
/// Provides thread-safe generation of random numbers.
/// </summary>
/// <remarks>This class ensures that random number generation is safe for use across multiple threads by utilizing
/// a thread-local instance of <see cref="System.Random"/> seeded with a globally synchronized random value. It offers
/// methods to generate random integers and doubles.
/// https://github.com/microsoft/FeatureManagement-Dotnet/blob/main/src/Microsoft.FeatureManagement/Utils/RandomGenerator.cs
/// </remarks>
internal static class RandomGenerator {
    private static readonly Random _Global = new();

    private static readonly ThreadLocal<Random> _Rnd = new(() => {
        int seed;

        lock (_Global) {
            seed = _Global.Next();
        }

        return new Random(seed);
    });

    public static int Next() => _Rnd.Value!.Next();

    public static double NextDouble() => _Rnd.Value!.NextDouble();
}
