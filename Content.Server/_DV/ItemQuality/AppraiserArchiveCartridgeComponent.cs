namespace Content.Server._DV.ItemQuality;

/// <summary>
/// Server-side component for the Appraiser Archive PDA cartridge.
/// Stores the last 15 scanned items and up to 5 favorites.
/// Items are added when examined through appraiser glasses.
/// </summary>
[RegisterComponent]
public sealed partial class AppraiserArchiveCartridgeComponent : Component
{
    /// <summary>
    /// Maximum number of recent scan entries to keep.
    /// </summary>
    [DataField]
    public int MaxEntries = 10;

    /// <summary>
    /// Maximum number of favorite entries allowed.
    /// </summary>
    [DataField]
    public int MaxFavorites = 5;

    /// <summary>
    /// The list of scanned item entries, newest first.
    /// </summary>
    [DataField]
    public List<Content.Shared._DV.ItemQuality.AppraiserArchiveEntry> Entries = new();
}
