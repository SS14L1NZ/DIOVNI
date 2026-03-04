using Content.Server.CartridgeLoader;
using Content.Shared._DV.ItemQuality;
using Content.Shared.CartridgeLoader;

namespace Content.Server._DV.ItemQuality;

/// <summary>
/// Server system for the Appraiser Archive PDA cartridge.
/// Manages the scan history and responds to UI messages.
/// Items are added to the archive by the ItemQualitySystem when examined through appraiser glasses.
/// </summary>
public sealed class AppraiserArchiveCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AppraiserArchiveCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<AppraiserArchiveCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
    }

    private void OnUiReady(EntityUid uid, AppraiserArchiveCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnUiMessage(EntityUid uid, AppraiserArchiveCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not AppraiserArchiveUiMessageEvent message)
            return;

        switch (message.Action)
        {
            case AppraiserArchiveAction.ToggleFavorite:
                if (message.EntryIndex >= 0 && message.EntryIndex < component.Entries.Count)
                {
                    var entry = component.Entries[message.EntryIndex];

                    // Can only add favorites up to the limit
                    if (!entry.IsFavorite)
                    {
                        var currentFavorites = 0;
                        foreach (var e in component.Entries)
                        {
                            if (e.IsFavorite)
                                currentFavorites++;
                        }

                        if (currentFavorites >= component.MaxFavorites)
                            break;
                    }

                    entry.IsFavorite = !entry.IsFavorite;
                }
                break;

            case AppraiserArchiveAction.RemoveEntry:
                if (message.EntryIndex >= 0 && message.EntryIndex < component.Entries.Count)
                {
                    component.Entries.RemoveAt(message.EntryIndex);
                }
                break;

            case AppraiserArchiveAction.ClearAll:
                // Keep favorites, remove everything else
                component.Entries.RemoveAll(e => !e.IsFavorite);
                break;

            case AppraiserArchiveAction.DecryptEntry:
                if (message.EntryIndex >= 0 && message.EntryIndex < component.Entries.Count)
                {
                    component.Entries[message.EntryIndex].IsDecrypted = true;
                }
                break;
        }

        UpdateUiState(uid, GetEntity(args.LoaderUid), component);
    }

    /// <summary>
    /// Adds a scanned item to the archive cartridge in the loader's PDA.
    /// Called by ItemQualitySystem when an item is examined through appraiser glasses.
    /// Uses CartridgeLoaderSystem.TryGetProgram to find the cartridge directly from the loader
    /// instead of iterating all archive cartridges in the world.
    /// </summary>
    public void AddScanEntry(EntityUid loaderUid, AppraiserArchiveEntry entry)
    {
        if (!_cartridgeLoader.TryGetProgram<AppraiserArchiveCartridgeComponent>(
                loaderUid, out var cartUid, out var archive))
            return;

        // Add at the beginning (newest first)
        archive.Entries.Insert(0, entry);

        // Trim to max entries (keep favorites from being trimmed)
        while (archive.Entries.Count > archive.MaxEntries)
        {
            // Find the last non-favorite entry to remove
            var lastNonFav = archive.Entries.FindLastIndex(e => !e.IsFavorite);
            if (lastNonFav >= 0)
                archive.Entries.RemoveAt(lastNonFav);
            else
                break; // all remaining are favorites, stop trimming
        }

        UpdateUiState(cartUid.Value, loaderUid, archive);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, AppraiserArchiveCartridgeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = new AppraiserArchiveUiState(component.Entries);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }
}
