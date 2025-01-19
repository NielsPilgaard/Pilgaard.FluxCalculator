namespace Pilgaard.EddyCovariance.Corrections;

internal static class FluxFootprint
{
	// TODO: Make less assumptions

	// TODO: Make configurable
	private const double VonKarman = 0.41;

	/// <summary>
	/// Calculates a simplified flux footprint distance for quality control purposes.
	/// This implementation provides a basic estimate based on Kljun et al. (2015) 
	/// under neutral atmospheric conditions.
	/// </summary>
	/// <remarks>
	/// Key assumptions and simplifications:
	/// 1. Neutral atmospheric stability (no buoyancy effects)
	/// 2. Logarithmic wind profile (constant flux layer)
	/// 3. Homogeneous surface conditions
	/// 4. No consideration of atmospheric boundary layer height
	/// 
	/// The footprint distance is estimated using the relationship between measurement height,
	/// surface roughness, and wind characteristics. This simplified model is suitable for:
	/// - Basic quality control of flux measurements
	/// - Initial assessment of source area
	/// - Homogeneous sites with relatively flat terrain
	/// 
	/// For more detailed footprint analysis, especially in complex terrain or strongly
	/// non-neutral conditions, consider using specialized footprint models.
	/// </remarks>
	/// <param name="meanWind">Mean wind speed [m/s]</param>
	/// <param name="measurementHeight">Measurement height [m]</param>
	/// <param name="roughnessLength">Surface roughness length [m]. If null, estimated as measurementHeight/10</param>
	/// <returns>Estimated peak footprint distance [m]</returns>
	internal static double Calculate(
		double meanWind,
		double measurementHeight,
		double? roughnessLength = null)
	{
		var z0 = roughnessLength ?? measurementHeight / 10.0;

		// Calculate friction velocity using log law approximation
		var uStar = meanWind * VonKarman / Math.Log(measurementHeight / z0);

		// Simplified neutral condition estimate of peak footprint distance
		// Following Kljun et al. (2015) for neutral conditions
		return measurementHeight * (2.0 * meanWind / uStar) *
			   (1.0 - Math.Exp(-1.5 * measurementHeight / z0));
	}
}