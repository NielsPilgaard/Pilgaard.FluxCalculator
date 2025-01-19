namespace Pilgaard.EddyCovariance.Corrections;

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