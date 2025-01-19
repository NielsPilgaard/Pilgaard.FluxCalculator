namespace Pilgaard.EddyCovariance.Corrections;

public class Despiking
{
	internal static (double[] cleanU, double[] cleanV, double[] cleanW, double[] cleanT,
	(int totalSpikes, double spikePercentage) stats) RemoveSpikes(
	ReadOnlySpan<double> u, ReadOnlySpan<double> v, ReadOnlySpan<double> w, ReadOnlySpan<double> t)
	{
		// Constants from Vickers and Mahrt (1997)
		const int windowSize = 10; // Size of moving window
		const double spikeThreshold = 3.5; // Number of standard deviations
		const int consecutivePoints = 3; // Number of consecutive points for spike detection

		var length = u.Length;
		var cleanU = u.ToArray();
		var cleanV = v.ToArray();
		var cleanW = w.ToArray();
		var cleanT = t.ToArray();

		Span<bool> spikeMask = new bool[length];
		var totalSpikes = 0;

		// Process each variable separately
		DetectSpikes(cleanU, spikeMask, windowSize, spikeThreshold, consecutivePoints);
		DetectSpikes(cleanV, spikeMask, windowSize, spikeThreshold, consecutivePoints);
		DetectSpikes(cleanW, spikeMask, windowSize, spikeThreshold, consecutivePoints);
		DetectSpikes(cleanT, spikeMask, windowSize, spikeThreshold, consecutivePoints);

		// Count total spikes and replace them with interpolated values
		for (var i = 0; i < length; i++)
		{
			if (!spikeMask[i])
			{
				continue;
			}

			totalSpikes++;
			var (prevIdx, nextIdx) = FindNearestNonSpikes(spikeMask, i);

			// Linear interpolation for all variables
			var weight = (i - prevIdx) / (double)(nextIdx - prevIdx);

			cleanU[i] = LinearInterpolate(cleanU[prevIdx], cleanU[nextIdx], weight);
			cleanV[i] = LinearInterpolate(cleanV[prevIdx], cleanV[nextIdx], weight);
			cleanW[i] = LinearInterpolate(cleanW[prevIdx], cleanW[nextIdx], weight);
			cleanT[i] = LinearInterpolate(cleanT[prevIdx], cleanT[nextIdx], weight);
		}

		return (cleanU, cleanV, cleanW, cleanT,
			(totalSpikes, spikePercentage: 100.0 * totalSpikes / length));
	}

	private static void DetectSpikes(double[] data, Span<bool> spikeMask, int windowSize,
		double threshold, int consecutivePoints)
	{
		var length = data.Length;
		var halfWindow = windowSize / 2;

		for (var i = halfWindow; i < length - halfWindow; i++)
		{
			// Extract window
			var window = new double[windowSize];
			Array.Copy(data, i - halfWindow, window, 0, windowSize);

			// Calculate statistics
			var mean = 0.0;
			var sumSq = 0.0;

			for (var j = 0; j < windowSize; j++)
			{
				mean += window[j];
				sumSq += window[j] * window[j];
			}

			mean /= windowSize;
			var variance = sumSq / windowSize - mean * mean;
			var stdDev = Math.Sqrt(variance);

			// Check for spikes
			var deviation = Math.Abs(data[i] - mean);
			if (deviation < threshold * stdDev)
			{
				continue;
			}

			// Check for consecutive points
			var spikeCount = 1;
			for (var j = 1; j < consecutivePoints && i + j < length; j++)
			{
				if (Math.Abs(data[i + j] - mean) > threshold * stdDev)
				{
					spikeCount++;
				}
			}

			if (spikeCount >= consecutivePoints)
			{
				spikeMask[i] = true;
			}
		}
	}

	private static (int Previous, int Next) FindNearestNonSpikes(ReadOnlySpan<bool> spikeMask, int currentIndex)
	{
		var prev = currentIndex;
		var next = currentIndex;

		// Find previous non-spike point
		while (prev > 0 && spikeMask[prev])
		{
			prev--;
		}

		// Find next non-spike point
		while (next < spikeMask.Length - 1 && spikeMask[next])
		{
			next++;
		}

		return (prev, next);
	}

	private static double LinearInterpolate(double start, double end, double weight)
	{
		return start + (end - start) * weight;
	}
}