namespace BBF.Shared.Contracts.MealPlanning;

public interface IGrocerySectionService
{
    Task<List<GrocerySectionDto>> GetSectionsAsync(int groupId, CancellationToken ct = default);
    Task<GrocerySectionDto> CreateAsync(int groupId, CreateGrocerySectionRequest request, CancellationToken ct = default);
    Task UpdateAsync(int groupId, int sectionId, UpdateGrocerySectionRequest request, CancellationToken ct = default);
    Task DeleteAsync(int groupId, int sectionId, CancellationToken ct = default);

    /// <summary>Persists a new sort order for a group's sections, given the full ordered list of IDs.</summary>
    Task ReorderAsync(int groupId, ReorderGrocerySectionsRequest request, CancellationToken ct = default);
}
