# Pilgaard.EddyCovariance

A .NET library for processing eddy covariance data, focusing on accurate calculation of surface-atmosphere fluxes. Currently implements sensible heat flux calculations with standard corrections and quality control procedures.

## Features

- Sensible heat flux calculation using eddy covariance method
- Data quality control including:
  - Despiking
  - Coordinate rotation
  - Stationarity checks
  - Turbulence development tests
- Flux footprint estimation
- Webb-Pearman-Leuning density correction

## Installation

```bash
dotnet add package Pilgaard.EddyCovariance
```

## Quick Start

```csharp
using Pilgaard.EddyCovariance;

// Assuming 30 minutes of 10Hz data
var fluxResult = FluxCalculator.CalculateSensibleHeatFlux(
    uWind: windData.U,
    vWind: windData.V,
    wWind: windData.W,
    sonicTemp: temperature,
    measurementHeight: 3.0, // meters
    samplingFrequency: 10.0 // Hz
);

Console.WriteLine($"Sensible heat flux: {fluxResult.Value} {fluxResult.Unit}");
Console.WriteLine($"Quality flags: {fluxResult.QualityFlags}");
```

## Quality Control

The package implements several quality control procedures:

1. **Stationarity Test**: Implementation based on:
   > Foken, T., & Wichura, B. (1996). Tools for quality assessment of surface-based flux measurements. Agricultural and Forest Meteorology, 78(1-2), 83-105. https://doi.org/10.1016/0168-1923(95)02248-1

2. **Integral Turbulence Characteristics**: Based on:
   > Foken, T., Göockede, M., Mauder, M., Mahrt, L., Amiro, B., & Munger, W. (2004). Post-field data quality control. In Handbook of micrometeorology (pp. 181-208). Springer, Dordrecht. https://doi.org/10.1007/1-4020-2265-4_9

3. **Flux Footprint Model**: Simplified implementation based on:
   > Kljun, N., Calanca, P., Rotach, M. W., & Schmid, H. P. (2015). A simple two-dimensional parameterisation for Flux Footprint Prediction (FFP). Geoscientific Model Development, 8(11), 3695-3713. https://doi.org/10.5194/gmd-8-3695-2015

## Technical Details

### Coordinate Rotation

Two methods are available for coordinate system alignment:

1. **Double Rotation**: Rotates the coordinate system to align with mean wind flow by:
   - First rotation: Aligns mean lateral wind (v) to zero
   - Second rotation: Aligns mean vertical wind (w) to zero
   
   Based on:
   > Kaimal, J. C., & Finnigan, J. J. (1994). Atmospheric boundary layer flows: their structure and measurement. Oxford university press.
   
2. **Planar Fit**: Determines the mean streamline plane over longer periods, particularly useful for:
   - Complex terrain
   - Sites with systematic tilt
   - When flow distortion is suspected
   
   Based on:
   > Wilczak, J. M., Oncley, S. P., & Stage, S. A. (2001). Sonic anemometer tilt correction algorithms. Boundary-Layer Meteorology, 99(1), 127-150. https://doi.org/10.1023/A:1018966204465

Use `RotationCorrection.CompareRotationMethods()` to analyze which method is most suitable for your site. The comparison implements methods described in:
   > Lee, X., Massman, W., & Law, B. (2004). Coordinate systems and rotation. In Handbook of micrometeorology (pp. 33-66). Springer, Dordrecht. https://doi.org/10.1007/1-4020-2265-4_3

Generally:
- Double rotation is suitable for flat, homogeneous terrain
- Planar fit is preferred for complex terrain or when the sonic anemometer's mounting angle is uncertain

### Density Corrections

A simplified Webb-Pearman-Leuning correction is applied to account for density fluctuations. 

Planned: For more accurate results in humid conditions, you will be able to include water vapor measurements.

### Constants

- Air density: 1.225 kg/m³ (at 15°C and 1013.25 hPa)
- Specific heat capacity of air: 1004.0 J/(kg·K)
- von Kármán constant: 0.41

## Contributing

Contributions are welcome!

## Planned Features

- [ ] Options for disabling QA/QC steps
- [ ] Configurable constants
- [ ] Latent heat flux calculations
- [ ] CO₂ flux calculations
- [ ] N₂O flux calculations
- [ ] Advanced stability corrections
- [ ] Full Webb-Pearman-Leuning corrections
- [ ] Storage term calculations
- [ ] Spectral corrections

## License

This project is currently unlicensed.

## Acknowledgments

This package implements methods developed by the eddy covariance community over several decades. Key methodological papers are cited in the relevant sections above.

## Citation

If you use this package in your research, please cite both the package and the relevant methodological papers listed above.

```bibtex
@software{pilgaard_eddycovariance_2024,
  author       = {Pilgaard, Niels},
  title        = {Pilgaard.EddyCovariance: A .NET Library for Eddy Covariance Calculations},
  year         = {2024},
  publisher    = {GitHub},
  url          = {https://github.com/NielsPilgaard/Pilgaard.EddyCovariance}
}
```
