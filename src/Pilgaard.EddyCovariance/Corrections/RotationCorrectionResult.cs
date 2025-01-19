namespace Pilgaard.EddyCovariance.Corrections;

public readonly ref struct RotationCorrectionResult
{
    public required ReadOnlySpan<double> RotatedU { get; init; }
    public required ReadOnlySpan<double> RotatedV { get; init; }
    public required ReadOnlySpan<double> RotatedW { get; init; }
    public required double AlphaDegrees { get; init; }
    public required double BetaDegrees { get; init; }
    public required RotationQualityFlags QualityFlags { get; init; }
}