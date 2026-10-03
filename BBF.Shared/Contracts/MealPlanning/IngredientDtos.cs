namespace BBF.Shared.Contracts.MealPlanning;

public record IngredientDto(
    int Id,
    string Name,
    string MeasurementType,
    int? SectionId,
    string? SectionName,
    decimal? PackageQuantity,
    string? PackageUnit);

public record CreateIngredientRequest(
    string Name,
    string MeasurementType,
    int? SectionId,
    decimal? PackageQuantity,
    string? PackageUnit);

public record UpdateIngredientRequest(
    string Name,
    string MeasurementType,
    int? SectionId,
    decimal? PackageQuantity,
    string? PackageUnit);
