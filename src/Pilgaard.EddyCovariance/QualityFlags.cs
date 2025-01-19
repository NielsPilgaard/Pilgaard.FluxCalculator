namespace Pilgaard.EddyCovariance;

[Flags]
public enum QualityFlags
{
    None = 0,
    Valid = 1,
    SpikesDetected = 1 << 1,
    NonStationaryConditions = 1 << 2,
    WeakTurbulence = 1 << 3,
    AngleOfAttackExceeded = 1 << 4,
    RainDetected = 1 << 5,
    OutsideFluxFootprint = 1 << 6,
}