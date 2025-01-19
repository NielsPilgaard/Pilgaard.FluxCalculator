using BenchmarkDotNet.Attributes;

[HideColumns("Job", "Error", "StdDev", "Median", "RatioSD")]
[SimpleJob]
public class Benchmarks
{
	[Params(18000, 36000, 72000)] // 30m, 60m, 120m with 10 Hz data
	public int ArraySize;

	[GlobalSetup]
	public void GlobalSetup()
	{
	}

	[Benchmark]
	public void EddyCovariance_SensibleHeatFlux_AllQualityControl()
	{
	}
}