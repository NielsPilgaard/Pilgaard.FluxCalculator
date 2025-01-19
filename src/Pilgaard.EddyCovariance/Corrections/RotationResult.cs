namespace Pilgaard.EddyCovariance.Corrections;

internal readonly ref struct RotationResult
{
    public ReadOnlySpan<double> RotatedU { get; init; }
    public ReadOnlySpan<double> RotatedV { get; init; }
    public ReadOnlySpan<double> RotatedW { get; init; }
    public (double Alpha, double Beta) Angles { get; init; }
}