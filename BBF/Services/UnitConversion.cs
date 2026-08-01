namespace BBF.Services;

/// <summary>
/// Fixed unit table for recipe/grocery quantities — not user-editable, unlike GrocerySection.
/// Conversion and aggregation only ever happens within the same MeasurementType: Volume units convert
/// to Volume units, Weight to Weight, Count to Count. There is no Volume&lt;-&gt;Weight conversion since
/// that requires per-ingredient density data recipes don't provide.
/// </summary>
public static class UnitConversion
{
    private record UnitInfo(string MeasurementType, decimal FactorToBase);

    // Base units: Teaspoon for Volume, Gram for Weight, Each for Count.
    private static readonly Dictionary<string, UnitInfo> Units = new()
    {
        ["Teaspoon"] = new("Volume", 1m),
        ["Tablespoon"] = new("Volume", 3m),
        ["FluidOunce"] = new("Volume", 6m),
        ["Cup"] = new("Volume", 48m),
        ["Pint"] = new("Volume", 96m),
        ["Quart"] = new("Volume", 192m),
        ["Gallon"] = new("Volume", 768m),

        ["Gram"] = new("Weight", 1m),
        ["Kilogram"] = new("Weight", 1000m),
        ["Ounce"] = new("Weight", 28.349523125m),
        ["Pound"] = new("Weight", 453.59237m),

        ["Each"] = new("Count", 1m),
    };

    public static IReadOnlyCollection<string> AllUnits => Units.Keys;

    public static string MeasurementTypeOf(string unit) => Units[unit].MeasurementType;

    public static IEnumerable<string> UnitsFor(string measurementType) =>
        Units.Where(u => u.Value.MeasurementType == measurementType).Select(u => u.Key);

    public static bool AreCompatible(string unitA, string unitB) =>
        Units[unitA].MeasurementType == Units[unitB].MeasurementType;

    /// <summary>Converts a quantity in the given unit to the type's base unit (Teaspoon/Gram/Each).</summary>
    public static decimal ToBase(decimal quantity, string unit) => quantity * Units[unit].FactorToBase;

    /// <summary>Converts a base-unit quantity into the given target unit. Caller must ensure the target
    /// unit's MeasurementType matches the quantity's origin (use AreCompatible/MeasurementTypeOf to check).</summary>
    public static decimal FromBase(decimal baseQuantity, string targetUnit) => baseQuantity / Units[targetUnit].FactorToBase;
}
