using Microsoft.Extensions.DependencyInjection;
using ParkingOCR.Core.Services;

namespace ParkingOCR.Core.Extensions;

/// <summary>
/// Dependency injection extension methods for ParkingOCR.Core.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all ParkingOCR services into the IServiceCollection.
    /// </summary>
    public static IServiceCollection AddParkingOcr(this IServiceCollection services, string? customOnnxModelPath = null)
    {
        services.AddSingleton<IEuropeanPlateValidator, EuropeanPlateValidator>();
        services.AddSingleton<IRomanianPlateValidator, RomanianPlateValidator>();
        services.AddSingleton<IPlateDetector, OnnxPlateDetector>();
        services.AddSingleton<IPlateSequenceOcr, OnnxSequenceOcr>();
        services.AddSingleton<ICharacterSegmenter, CharacterSegmenter>();
        services.AddSingleton<ICharacterClassifier>(_ => new OnnxCharacterClassifier(customOnnxModelPath));
        services.AddSingleton<ILicensePlateOcrService>(sp => new LicensePlateOcrService(
            sp.GetRequiredService<IPlateDetector>(),
            sp.GetRequiredService<ICharacterSegmenter>(),
            sp.GetRequiredService<ICharacterClassifier>(),
            sp.GetRequiredService<IEuropeanPlateValidator>(),
            sp.GetRequiredService<IPlateSequenceOcr>()));

        return services;
    }
}
