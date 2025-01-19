namespace Pilgaard.EddyCovariance;

public readonly record struct FluxResult
{
    public required double Value { get; init; }
    public required string Unit { get; init; }
    public required QualityFlags QualityFlags { get; init; }
    public required Dictionary<string, double> Diagnostics { get; init; }
}