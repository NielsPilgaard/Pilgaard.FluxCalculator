using System.ComponentModel.DataAnnotations;
using Pilgaard.EddyCovariance.Corrections;

namespace Pilgaard.EddyCovariance;
public class FluxCalculationOptions
{
	/// <summary>
	/// Required for performing Flux Footprint Quality Control.
	/// </summary>
	public double? MeasurementHeightMeters { get; set; }

	public double? RoughnessLengthMeters { get; set; }

	public RotationMethod RotationMethod { get; set; } = RotationMethod.DoubleRotation;
}
public static class Flux
{
	// TODO: Make configurable
	private const double AirDensity = 1.225; // kg/m³ at 15°C and 1013.25 hPa
	private const double SpecificHeatCapacity = 1004.0; // J/(kg·K) for dry air
	private const double MaxAngleOfAttack = 30.0; // degrees
	private const int MinDataPoints = 9000; // 15 minutes at 10 Hz

	public static FluxResult CalculateSensibleHeatFlux(
		ReadOnlySpan<double> uWind,
		ReadOnlySpan<double> vWind,
		ReadOnlySpan<double> wWind,
		ReadOnlySpan<double> sonicTemp,
		Action<FluxCalculationOptions>? configurator = null) // meters
	{
		var options = new FluxCalculationOptions();
		configurator?.Invoke(options);

		ValidateInputs(uWind, vWind, wWind, sonicTemp, options.MeasurementHeightMeters);

		var qualityFlags = QualityFlags.Valid;
		var diagnostics = new Dictionary<string, double>();

		// Step 1: Despiking
		var (cleanU, cleanV, cleanW, cleanT, spikeStats) = Despiking.RemoveSpikes(uWind, vWind, wWind, sonicTemp);
		if (spikeStats.totalSpikes > 0)
		{
			qualityFlags |= QualityFlags.SpikesDetected;
		}

		diagnostics.Add("spike_percentage", spikeStats.spikePercentage);

		// Step 2: Coordinate Rotation
		var rotationCorrectionResult = Rotation.ApplyCoordinateRotation(
			cleanU,
			cleanV,
			cleanW,
			options.RotationMethod);

		if (Math.Abs(rotationCorrectionResult.BetaDegrees) > MaxAngleOfAttack)
		{
			qualityFlags |= QualityFlags.AngleOfAttackExceeded;
		}

		diagnostics.Add("rotation_angle_alpha", rotationCorrectionResult.AlphaDegrees);
		diagnostics.Add("rotation_angle_beta", rotationCorrectionResult.BetaDegrees);

		// Step 3: Stationarity Test
		var isStationary = Stationarity.CheckStationarity(rotationCorrectionResult.RotatedW,
			cleanT,
			out var covariance);
		if (!isStationary)
		{
			qualityFlags |= QualityFlags.NonStationaryConditions;
		}

		// Step 4: Turbulence Test
		var hasSufficientTurbulence = Turbulence.CheckTurbulence(rotationCorrectionResult.RotatedU,
			rotationCorrectionResult.RotatedV,
			rotationCorrectionResult.RotatedW);
		if (!hasSufficientTurbulence)
		{
			qualityFlags |= QualityFlags.WeakTurbulence;
		}

		// Step 5: Flux Footprint
		if (options.MeasurementHeightMeters is not null)
		{
			var footprintDistance = FluxFootprint.Calculate(
				rotationCorrectionResult.RotatedU.Average(),
				options.MeasurementHeightMeters.Value,
				options.RoughnessLengthMeters);

			diagnostics.Add("flux_footprint_distance", footprintDistance);
		}

		// Step 6: Calculate Covariance and Flux
		var flux = CalculateFlux(covariance);

		// Step 7: Apply Webb-Pearman-Leuning Correction
		// Note: This is a simplified WPL correction. For more accurate results,
		// water vapor measurements should be included.
		flux *= 1.0 + 0.07; // Approximate 7% correction

		return new FluxResult
		{
			Value = flux,
			Unit = "W/m²",
			QualityFlags = qualityFlags,
			Diagnostics = diagnostics
		};
	}

	private static void ValidateInputs(
		ReadOnlySpan<double> u,
		ReadOnlySpan<double> v,
		ReadOnlySpan<double> w,
		ReadOnlySpan<double> t,
		double? height)
	{
		if (u.Length != v.Length || u.Length != w.Length || u.Length != t.Length)
		{
			throw new ValidationException("All input arrays must have the same length");
		}

		if (u.Length < MinDataPoints)
		{
			throw new ValidationException($"Minimum of {MinDataPoints} data points required");
		}

		if (height is not null and <= 0)
		{
			throw new ValidationException("Measurement height must be positive");
		}
	}

	internal static double CalculateFlux(double covariance)
		=> AirDensity * SpecificHeatCapacity * covariance;
}