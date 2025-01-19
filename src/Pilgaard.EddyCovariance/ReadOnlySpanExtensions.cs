namespace Pilgaard.EddyCovariance;

internal static class ReadOnlySpanExtensions
{
	internal static double Average(this ReadOnlySpan<double> span)
	{
		double sum = 0;
		foreach (var value in span)
		{
			sum += value;
		}

		return sum / span.Length;
	}

	internal static double CalculateStandardDeviation(this ReadOnlySpan<double> data, double? mean = null)
	{
		mean ??= data.Average();
		var sumSquaredDiff = 0.0;

		foreach (var value in data)
		{
			var diff = value - mean.Value;
			sumSquaredDiff += diff * diff;
		}

		return Math.Sqrt(sumSquaredDiff / (data.Length - 1));
	}

	internal static double CalculateCovariance(this ReadOnlySpan<double> w, ReadOnlySpan<double> t)
	{
		double sumW = 0;
		double sumT = 0;
		for (var i = 0; i < w.Length; i++)
		{
			sumW += w[i];
			sumT += t[i];
		}

		var meanW = sumW / w.Length;
		var meanT = sumT / t.Length;

		var covariance = 0.0;
		for (var i = 0; i < w.Length; i++)
		{
			var wPrime = w[i] - meanW;
			var tPrime = t[i] - meanT;
			covariance += wPrime * tPrime;
		}

		return covariance / w.Length;
	}
}