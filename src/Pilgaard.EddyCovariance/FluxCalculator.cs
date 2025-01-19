using System.ComponentModel.DataAnnotations;
using Pilgaard.EddyCovariance.Corrections;

namespace Pilgaard.EddyCovariance;

public static class FluxCalculator
{
	private const double AirDensity = 1.225; // kg/m³ at 15°C and 1013.25 hPa
	private const double SpecificHeatCapacity = 1004.0; // J/(kg·K) for dry air
	private const double MaxAngleOfAttack = 30.0; // degrees
	private const double StationarityThreshold = 0.3; // 30% difference threshold
	private const int MinDataPoints = 18000; // 30 minutes at 10 Hz

	public static FluxResult CalculateSensibleHeatFlux(
		ReadOnlySpan<double> uWind,
		ReadOnlySpan<double> vWind,
		ReadOnlySpan<double> wWind,
		ReadOnlySpan<double> sonicTemp,
		double measurementHeight, // meters
		double samplingFrequency, // Hz
		double? roughnessLength = null,
		RotationMethod rotationMethod = RotationMethod.PlanarFit) // meters
	{
		ValidateInputs(uWind, vWind, wWind, sonicTemp, measurementHeight, samplingFrequency);

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
		var rotationCorrectionResult = RotationCorrection.ApplyCoordinateRotation(
			cleanU,
			cleanV,
			cleanW,
			rotationMethod);

		if (Math.Abs(rotationCorrectionResult.BetaDegrees) > MaxAngleOfAttack)
		{
			qualityFlags |= QualityFlags.AngleOfAttackExceeded;
		}

		diagnostics.Add("rotation_angle_alpha", rotationCorrectionResult.AlphaDegrees);
		diagnostics.Add("rotation_angle_beta", rotationCorrectionResult.BetaDegrees);

		// Step 3: Stationarity Test
		var isStationary = CheckStationarity(rotationCorrectionResult.RotatedW, cleanT, out var covariance);
		if (!isStationary)
		{
			qualityFlags |= QualityFlags.NonStationaryConditions;
		}

		// Step 4: Turbulence Test
		var hasSufficientTurbulence = CheckTurbulence(rotationCorrectionResult.RotatedU,
			rotationCorrectionResult.RotatedV,
			rotationCorrectionResult.RotatedW);
		if (!hasSufficientTurbulence)
		{
			qualityFlags |= QualityFlags.WeakTurbulence;
		}

		// Step 5: Flux Footprint
		var footprintDistance = CalculateFluxFootprint(
			rotationCorrectionResult.RotatedU.Average(),
			measurementHeight,
			roughnessLength ?? EstimateRoughnessLength(measurementHeight));
		diagnostics.Add("flux_footprint_distance", footprintDistance);

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
		double height,
		double frequency)
	{
		if (u.Length != v.Length || u.Length != w.Length || u.Length != t.Length)
		{
			throw new ValidationException("All input arrays must have the same length");
		}

		if (u.Length < MinDataPoints)
		{
			throw new ValidationException($"Minimum of {MinDataPoints} data points required");
		}

		if (height <= 0)
		{
			throw new ValidationException("Measurement height must be positive");
		}

		if (frequency <= 0)
		{
			throw new ValidationException("Sampling frequency must be positive");
		}
	}

	internal static bool CheckStationarity(ReadOnlySpan<double> w, ReadOnlySpan<double> t, out double covariance)
	{
		// Implementation of Foken and Wichura (1996) stationarity test
		const int subPeriods = 6; // Divide into 5-minute segments for 30-min period
		var segmentLength = w.Length / subPeriods;

		// Calculate covariance for the entire period
		var totalCovariance = w.CalculateCovariance(t);
		covariance = totalCovariance;

		// Calculate mean of covariances from sub-periods
		var subPeriodCovariances = new double[subPeriods];
		for (var i = 0; i < subPeriods; i++)
		{
			var startIdx = i * segmentLength;
			var wSegment = w.Slice(startIdx, segmentLength);
			var tSegment = t.Slice(startIdx, segmentLength);
			subPeriodCovariances[i] = wSegment.CalculateCovariance(tSegment);
		}

		var meanSubPeriodCovariance = subPeriodCovariances.Average();

		// Calculate relative difference
		var relativeDifference = Math.Abs(totalCovariance - meanSubPeriodCovariance) /
								 Math.Abs(totalCovariance);

		return relativeDifference <= StationarityThreshold;
	}

	internal static bool CheckTurbulence(
		ReadOnlySpan<double> u, ReadOnlySpan<double> v, ReadOnlySpan<double> w)
	{
		// Implementation based on Integral Turbulence Characteristics (ITC)
		// Following Foken's quality classification scheme

		// Calculate standard deviations
		var sigmaU = u.CalculateStandardDeviation();
		var sigmaV = v.CalculateStandardDeviation();
		var sigmaW = w.CalculateStandardDeviation();

		// Calculate mean wind speed
		var meanU = u.Average();

		// Check if turbulence parameters are within expected ranges
		// These thresholds are based on typical values for well-developed turbulence
		const double minSigmaURatio = 0.5;  // σu/U
		const double maxSigmaURatio = 3.0;
		const double minSigmaVRatio = 0.5;  // σv/U
		const double maxSigmaVRatio = 2.5;
		const double minSigmaWRatio = 0.1;  // σw/U
		const double maxSigmaWRatio = 1.0;

		var sigmaURatio = sigmaU / meanU;
		var sigmaVRatio = sigmaV / meanU;
		var sigmaWRatio = sigmaW / meanU;

		return sigmaURatio is >= minSigmaURatio and <= maxSigmaURatio &&
			   sigmaVRatio is >= minSigmaVRatio and <= maxSigmaVRatio &&
			   sigmaWRatio is >= minSigmaWRatio and <= maxSigmaWRatio;
	}

	internal static double CalculateFluxFootprint(
		double meanWind, double measurementHeight, double roughnessLength)
	{
		// Implementation of simplified Kljun et al. (2015) flux footprint model
		// Returns the peak footprint distance (xmax)

		// Constants from the parameterization
		const double a = 1.452;
		const double b = -1.991;
		const double c = 1.462;

		// Calculate friction velocity using log law approximation
		const double vonKarman = 0.41;
		var uStar = meanWind * vonKarman / Math.Log(measurementHeight / roughnessLength);

		// Calculate Obukhov length (assuming neutral conditions as a simplification)
		// For more accurate results, temperature and heat flux measurements should be used
		const double obukhovLength = double.PositiveInfinity;

		// Calculate scaled height
		var zStar = measurementHeight / obukhovLength;

		// Calculate peak distance of flux footprint
		var xmax = measurementHeight * (a * Math.Log(measurementHeight / roughnessLength) +
										b * zStar + c);

		return xmax;
	}

	internal static double EstimateRoughnessLength(double measurementHeight)
	{
		// Simple estimation of z0 based on measurement height
		// More sophisticated methods could use land cover data
		return measurementHeight / 10.0;
	}

	internal static double CalculateFlux(double covariance)
	{
		return AirDensity * SpecificHeatCapacity * covariance;
	}
}