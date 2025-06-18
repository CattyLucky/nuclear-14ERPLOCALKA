using Content.Shared._NC.Trade.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._NC.Trade.Systems;

public sealed class StoreSystemStructuredLoader : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protos = default!;


    public void ApplyPreset(EntityUid storeEntity, string presetId)
    {
        if (!_protos.TryIndex<StorePresetStructuredPrototype>(presetId, out var preset))
            return;


        var comp = EnsureComp<StoreStructuredComponent>(storeEntity);
        comp.PresetId = presetId;
        comp.ListingIds.Clear();
        comp.ListingIds.AddRange(preset.Listings);
        Dirty(storeEntity, comp);
    }
}

