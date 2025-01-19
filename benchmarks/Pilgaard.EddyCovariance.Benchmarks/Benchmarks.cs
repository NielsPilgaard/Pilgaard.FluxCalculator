using BenchmarkDotNet.Attributes;


[HideColumns("Job", "Error", "StdDev", "Median", "RatioSD")]
[SimpleJob]
public class Benchmarks
{
    [Params(9000, 18000)]
    public int ArraySize;

    // Input data
    public double[] SonicTemperatures = null!;
    public double[] UWindSpeeds = null!;
    public double[] VWindSpeeds = null!;
    public double[] WWindSpeeds = null!;

    // Constants
    private const double AirDensity = 1.225; // kg/m³ at 15°C and 1013.25 hPa
    private const double SpecificHeatCapacity = 1004.0; // J/(kg·K) for dry air

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = Random.Shared;

        // Initialize arrays with realistic values
        SonicTemperatures = new double[ArraySize];
        UWindSpeeds = new double[ArraySize];
        VWindSpeeds = new double[ArraySize];
        WWindSpeeds = new double[ArraySize];

        for (var i = 0; i < ArraySize; i++)
        {
            // Temperature typically between 0 and 40°C
            SonicTemperatures[i] = random.NextDouble() * 40.0;

            // Horizontal wind components typically between -20 and 20 m/s
            UWindSpeeds[i] = (random.NextDouble() - 0.5) * 40.0;
            VWindSpeeds[i] = (random.NextDouble() - 0.5) * 40.0;

            // Vertical wind typically smaller, between -5 and 5 m/s
            WWindSpeeds[i] = (random.NextDouble() - 0.5) * 10.0;
        }
    }

    [Benchmark]
    public (double sensibleHeatFlux, double[] rotatedW) EddyCovariance_SensibleHeatFlux()
    {
        // Step 1: Calculate means
        var meanU = UWindSpeeds.Average();
        var meanV = VWindSpeeds.Average();
        var meanW = WWindSpeeds.Average();
        var meanTemp = SonicTemperatures.Average();

        // Step 2: Calculate rotation angles
        var alpha = Math.Atan2(meanV, meanU);
        var horizontalWindSpeed = Math.Sqrt(meanU * meanU + meanV * meanV);
        var beta = Math.Atan2(meanW, horizontalWindSpeed);

        // Step 3: Apply coordinate rotation
        var rotatedU = new double[ArraySize];
        var rotatedV = new double[ArraySize];
        var rotatedW = new double[ArraySize];

        for (var i = 0; i < ArraySize; i++)
        {
            // First rotation around z-axis (alpha)
            var u1 = UWindSpeeds[i] * Math.Cos(alpha) + VWindSpeeds[i] * Math.Sin(alpha);
            var v1 = -UWindSpeeds[i] * Math.Sin(alpha) + VWindSpeeds[i] * Math.Cos(alpha);
            var w1 = WWindSpeeds[i];

            // Second rotation around y-axis (beta)
            rotatedU[i] = u1 * Math.Cos(beta) + w1 * Math.Sin(beta);
            rotatedV[i] = v1;
            rotatedW[i] = -u1 * Math.Sin(beta) + w1 * Math.Cos(beta);
        }

        // Step 4: Calculate fluctuations and covariance with rotated components
        var sumProduct = 0.0;
        var meanRotatedW = rotatedW.Average();

        for (var i = 0; i < ArraySize; i++)
        {
            var tempFluctuation = SonicTemperatures[i] - meanTemp;
            var wFluctuation = rotatedW[i] - meanRotatedW;
            sumProduct += wFluctuation * tempFluctuation;
        }

        var covariance = sumProduct / ArraySize;
        var sensibleHeatFlux = AirDensity * SpecificHeatCapacity * covariance;

        return (sensibleHeatFlux, rotatedW);
    }

    [Benchmark]
    public (double sensibleHeatFlux, double[] rotatedW) EddyCovariance_SensibleHeatFlux_WithTensors()
    {
        // Step 1: Calculate means
        var meanU = UWindSpeeds.Average();
        var meanV = VWindSpeeds.Average();
        var meanW = WWindSpeeds.Average();
        var meanTemp = SonicTemperatures.Average();

        // Step 2: Calculate rotation angles
        var alpha = Math.Atan2(meanV, meanU);
        var horizontalWindSpeed = Math.Sqrt(meanU * meanU + meanV * meanV);
        var beta = Math.Atan2(meanW, horizontalWindSpeed);

        // Step 3: Apply coordinate rotation
        var rotatedU = new double[ArraySize];
        var rotatedV = new double[ArraySize];
        var rotatedW = new double[ArraySize];

        for (var i = 0; i < ArraySize; i++)
        {
            // First rotation around z-axis (alpha)
            var u1 = UWindSpeeds[i] * Math.Cos(alpha) + VWindSpeeds[i] * Math.Sin(alpha);
            var v1 = -UWindSpeeds[i] * Math.Sin(alpha) + VWindSpeeds[i] * Math.Cos(alpha);
            var w1 = WWindSpeeds[i];

            // Second rotation around y-axis (beta)
            rotatedU[i] = u1 * Math.Cos(beta) + w1 * Math.Sin(beta);
            rotatedV[i] = v1;
            rotatedW[i] = -u1 * Math.Sin(beta) + w1 * Math.Cos(beta);
        }

        // Step 4: Calculate fluctuations and covariance with rotated components
        var sumProduct = 0.0;
        var meanRotatedW = rotatedW.Average();

        for (var i = 0; i < ArraySize; i++)
        {
            var tempFluctuation = SonicTemperatures[i] - meanTemp;
            var wFluctuation = rotatedW[i] - meanRotatedW;
            sumProduct += wFluctuation * tempFluctuation;
        }

        var covariance = sumProduct / ArraySize;
        var sensibleHeatFlux = AirDensity * SpecificHeatCapacity * covariance;

        return (sensibleHeatFlux, rotatedW);
    }
}