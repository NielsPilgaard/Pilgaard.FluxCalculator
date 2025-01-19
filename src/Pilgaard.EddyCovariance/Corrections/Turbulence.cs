namespace Pilgaard.EddyCovariance.Corrections;
internal class Turbulence
{
	// TODO: Make configurable
	// These thresholds are based on typical values for well-developed turbulence
	private const double minSigmaURatio = 0.5;  // σu/U
	private const double maxSigmaURatio = 3.0;
	private const double minSigmaVRatio = 0.5;  // σv/U
	private const double maxSigmaVRatio = 2.5;
	private const double minSigmaWRatio = 0.1;  // σw/U
	private const double maxSigmaWRatio = 1.0;

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
		var sigmaURatio = sigmaU / meanU;
		var sigmaVRatio = sigmaV / meanU;
		var sigmaWRatio = sigmaW / meanU;

		return sigmaURatio is >= minSigmaURatio and <= maxSigmaURatio &&
			   sigmaVRatio is >= minSigmaVRatio and <= maxSigmaVRatio &&
			   sigmaWRatio is >= minSigmaWRatio and <= maxSigmaWRatio;
	}
}
