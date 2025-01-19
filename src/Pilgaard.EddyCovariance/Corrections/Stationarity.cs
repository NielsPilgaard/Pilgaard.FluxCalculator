namespace Pilgaard.EddyCovariance.Corrections;
internal class Stationarity
{
	// TODO: Make configurable
	private const double StationarityThreshold = 0.3; // 30% difference threshold

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
}
