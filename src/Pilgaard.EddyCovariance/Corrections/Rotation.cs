namespace Pilgaard.EddyCovariance.Corrections;

public static class Rotation
{
	// TODO: Make configurable
	// Thresholds based on literature and common practice
	private const double VeryLowWindSpeed = 0.05; // m/s - may need special handling
	private const double LowWindSpeed = 0.3; // m/s - flag but process

	internal static RotationResult ApplyDoubleRotation(
		ReadOnlySpan<double> u, ReadOnlySpan<double> v, ReadOnlySpan<double> w,
		double meanU, double meanV, double meanW, double horizontalWindSpeed)
	{
		var length = u.Length;
		var rotatedU = new double[length];
		var rotatedV = new double[length];
		var rotatedW = new double[length];

		// First rotation angle (alpha) - rotate into mean wind
		var alpha = Math.Atan2(meanV, meanU);

		// Second rotation angle (beta) - rotate to make mean vertical wind zero
		var beta = Math.Atan2(meanW, horizontalWindSpeed);

		// Check for extreme rotation angles
		const double maxRotationAngle = 45.0 * Math.PI / 180.0; // 45 degrees in radians
		if (Math.Abs(beta) > maxRotationAngle)
		{
			throw new InvalidOperationException(
				$"Extreme rotation angle detected: {beta * 180 / Math.PI:F1} degrees");
		}

		// Precalculate trigonometric functions
		var cosAlpha = Math.Cos(alpha);
		var sinAlpha = Math.Sin(alpha);
		var cosBeta = Math.Cos(beta);
		var sinBeta = Math.Sin(beta);

		// Apply rotations
		for (var i = 0; i < length; i++)
		{
			// First rotation (alpha)
			var u1 = u[i] * cosAlpha + v[i] * sinAlpha;
			var v1 = -u[i] * sinAlpha + v[i] * cosAlpha;
			var w1 = w[i];

			// Second rotation (beta)
			rotatedU[i] = u1 * cosBeta + w1 * sinBeta;
			rotatedV[i] = v1;
			rotatedW[i] = -u1 * sinBeta + w1 * cosBeta;
		}

		return new RotationResult
		{
			RotatedU = rotatedU,
			RotatedV = rotatedV,
			RotatedW = rotatedW,
			Angles = (alpha * 180 / Math.PI, beta * 180 / Math.PI)
		};
	}

	private static RotationResult ApplyPlanarFitRotation(
		ReadOnlySpan<double> u, ReadOnlySpan<double> v, ReadOnlySpan<double> w,
		double meanU, double meanV, double meanW)
	{
		var length = u.Length;

		// Step 1: Build the system of equations for planar fit
		// w = b0 + b1*u + b2*v
		double sumU2 = 0, sumV2 = 0, sumUV = 0, sumUW = 0, sumVW = 0;
		for (var i = 0; i < length; i++)
		{
			var uDev = u[i] - meanU;
			var vDev = v[i] - meanV;
			var wDev = w[i] - meanW;

			sumU2 += uDev * uDev;
			sumV2 += vDev * vDev;
			sumUV += uDev * vDev;
			sumUW += uDev * wDev;
			sumVW += vDev * wDev;
		}

		// Solve the system using linear least squares
		var det = sumU2 * sumV2 - sumUV * sumUV;
		if (Math.Abs(det) < 1e-10)
		{
			throw new InvalidOperationException(
				"Singular matrix encountered in planar fit calculation");
		}

		// Calculate regression coefficients
		var b1 = (sumUW * sumV2 - sumVW * sumUV) / det;
		var b2 = (sumVW * sumU2 - sumUW * sumUV) / det;

		// Calculate rotation angles
		var beta = Math.Atan(b1 / Math.Sqrt(1 + b1 * b1 + b2 * b2));
		var alpha = Math.Atan(b2 / Math.Sqrt(1 + b2 * b2));

		// Check for extreme angles
		const double maxRotationAngle = 45.0 * Math.PI / 180.0;
		if (Math.Abs(beta) > maxRotationAngle || Math.Abs(alpha) > maxRotationAngle)
		{
			throw new InvalidOperationException(
				$"Extreme rotation angles detected: alpha={alpha * 180 / Math.PI:F1}°, beta={beta * 180 / Math.PI:F1}°");
		}

		// Precalculate rotation matrices
		var cosBeta = Math.Cos(beta);
		var sinBeta = Math.Sin(beta);
		var cosAlpha = Math.Cos(alpha);
		var sinAlpha = Math.Sin(alpha);

		var rotatedU = new double[length];
		var rotatedV = new double[length];
		var rotatedW = new double[length];

		// Apply rotation to all points
		for (var i = 0; i < length; i++)
		{
			// Apply combined rotation matrix
			rotatedU[i] = u[i] * cosAlpha * cosBeta +
						  v[i] * sinAlpha * cosBeta +
						  w[i] * sinBeta;
			rotatedV[i] = -u[i] * sinAlpha +
						  v[i] * cosAlpha;
			rotatedW[i] = -u[i] * cosAlpha * sinBeta -
						  v[i] * sinAlpha * sinBeta +
						  w[i] * cosBeta;
		}

		return new RotationResult
		{
			RotatedU = rotatedU,
			RotatedV = rotatedV,
			RotatedW = rotatedW,
			Angles = (alpha * 180 / Math.PI, beta * 180 / Math.PI)
		};
	}

	public static RotationMethod RecommendRotationMethod(
		TerrainType terrain,
		double averageWindSpeed,
		double terrainSlope = 0)
	{
		// Based on common guidelines in flux literature
		return terrain switch
		{
			TerrainType.Flat when terrainSlope < 5 => RotationMethod.DoubleRotation,
			TerrainType.Rolling when terrainSlope < 10 => RotationMethod.DoubleRotation,
			_ => RotationMethod.PlanarFit // Complex, Urban, or steep slopes
		};
	}

	internal static RotationCorrectionResult ApplyCoordinateRotation(
		ReadOnlySpan<double> u,
		ReadOnlySpan<double> v,
		ReadOnlySpan<double> w,
		RotationMethod method)
	{
		var length = u.Length;
		var qualityFlags = RotationQualityFlags.Valid;

		// Calculate means and check for wind conditions
		double sumU = 0, sumV = 0, sumW = 0;
		for (var i = 0; i < length; i++)
		{
			sumU += u[i];
			sumV += v[i];
			sumW += w[i];
		}

		var meanU = sumU / length;
		var meanV = sumV / length;
		var meanW = sumW / length;

		var horizontalWindSpeed = Math.Sqrt(meanU * meanU + meanV * meanV);

		switch (horizontalWindSpeed)
		{
			// Handle low wind speeds with flags instead of exceptions
			case < VeryLowWindSpeed:
				// For very low wind speeds, return non-rotated data
				return new RotationCorrectionResult
				{
					RotatedU = u,
					RotatedV = v,
					RotatedW = w,
					AlphaDegrees = 0,
					BetaDegrees = 0,
					QualityFlags = RotationQualityFlags.LowWindSpeed
				};
			case < LowWindSpeed:
				qualityFlags |= RotationQualityFlags.LowWindSpeed;
				break;
		}

		try
		{
			var rotationResult = method switch
			{
				RotationMethod.PlanarFit => ApplyPlanarFitRotation(u, v, w, meanU, meanV, meanW),
				_ => ApplyDoubleRotation(u, v, w, meanU, meanV, meanW, horizontalWindSpeed)
			};

			return new RotationCorrectionResult
			{
				RotatedU = rotationResult.RotatedU,
				RotatedV = rotationResult.RotatedV,
				RotatedW = rotationResult.RotatedW,
				AlphaDegrees = rotationResult.Angles.Alpha,
				BetaDegrees = rotationResult.Angles.Beta,
				QualityFlags = qualityFlags
			};
		}
		catch (Exception exception)
		{
			// If rotation fails, return non-rotated data with appropriate flags
			return new RotationCorrectionResult
			{
				RotatedU = u,
				RotatedV = v,
				RotatedW = w,
				AlphaDegrees = 0,
				BetaDegrees = 0,
				QualityFlags = qualityFlags | GetQualityFlagFromException(exception)
			};
		}
	}

	private static RotationQualityFlags GetQualityFlagFromException(Exception exception) =>
		exception switch
		{
			InvalidOperationException ex when ex.Message.Contains("singular")
				=> RotationQualityFlags.SingularMatrix,
			InvalidOperationException ex when ex.Message.Contains("angle")
				=> RotationQualityFlags.ExtremeRotationAngle,
			_ => RotationQualityFlags.None
		};
}

public readonly ref struct RotationCorrectionResult
{
	public required ReadOnlySpan<double> RotatedU { get; init; }
	public required ReadOnlySpan<double> RotatedV { get; init; }
	public required ReadOnlySpan<double> RotatedW { get; init; }
	public required double AlphaDegrees { get; init; }
	public required double BetaDegrees { get; init; }
	public required RotationQualityFlags QualityFlags { get; init; }
}

public enum RotationMethod
{
	DoubleRotation,
	PlanarFit
}

[Flags]
public enum RotationQualityFlags
{
	None = 0,
	Valid = 1,
	LowWindSpeed = 1 << 1,
	ExtremeRotationAngle = 1 << 2,
	SingularMatrix = 1 << 3,
	ComplexTerrain = 1 << 4
}

internal readonly ref struct RotationResult
{
	public ReadOnlySpan<double> RotatedU { get; init; }
	public ReadOnlySpan<double> RotatedV { get; init; }
	public ReadOnlySpan<double> RotatedW { get; init; }
	public (double Alpha, double Beta) Angles { get; init; }
}

public enum TerrainType
{
	Flat,
	Rolling,
	Complex,
	Urban
}