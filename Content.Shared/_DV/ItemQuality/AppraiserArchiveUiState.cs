using Robust.Shared.Serialization;

namespace Content.Shared._DV.ItemQuality;

/// <summary>
/// An entry representing a scanned item's quality data for the appraiser archive cartridge.
/// Includes optional weapon and armor details for expanded view.
/// </summary>
[Serializable, NetSerializable]
public sealed class AppraiserArchiveEntry
{
    public string ItemName = string.Empty;
    public string ItemDescription = string.Empty;
    public ItemQualityTier Quality;
    public float Wear;
    public float EffectiveMultiplier;
    public bool IsFavorite;

    /// <summary>
    /// Item category for display (Weapon, Armor, Tool, etc.)
    /// </summary>
    public string ItemCategory = "Item";

    // ─── Weapon data (null if not a weapon) ───
    public float? MeleeDamage;
    public float? AdjustedMeleeDamage;
    public float? FireRate;

    /// <summary>
    /// Minimum gun spread angle in degrees.
    /// </summary>
    public float? GunMinAngle;

    /// <summary>
    /// Maximum gun spread angle in degrees.
    /// </summary>
    public float? GunMaxAngle;

    /// <summary>
    /// Projectile speed (units/sec).
    /// </summary>
    public float? ProjectileSpeed;

    /// <summary>
    /// Fire mode display string (SemiAuto / FullAuto / Burst).
    /// </summary>
    public string? GunFireMode;

    /// <summary>
    /// Rounds per burst (only if burst mode available).
    /// </summary>
    public int? ShotsPerBurst;

    /// <summary>
    /// Per-type melee damage breakdown (key = damage type, value = damage).
    /// </summary>
    public Dictionary<string, float>? MeleeDamageTypes;

    /// <summary>
    /// Whether this weapon can jam.
    /// </summary>
    public bool CanJam;

    /// <summary>
    /// Whether this weapon is currently jammed.
    /// </summary>
    public bool IsJammed;

    // ─── Armor data (null if not armor) ───
    public Dictionary<string, float>? ArmorCoefficients;
    public Dictionary<string, float>? ArmorFlatReduction;

    // ─── Extra stats (slowdown, explosion, stamina) ───
    /// <summary>
    /// Speed modifier percentage (positive = slowdown, negative = speed boost). Null if no effect.
    /// </summary>
    public float? SpeedModifierPercent;

    /// <summary>
    /// Explosion resistance percentage (0-100). Null if none.
    /// </summary>
    public float? ExplosionResistance;

    /// <summary>
    /// Stamina damage resistance percentage (0-100). Null if none.
    /// </summary>
    public float? StaminaResistance;

    // ─── Price estimate ───
    /// <summary>
    /// Estimated minimum market price.
    /// </summary>
    public int? EstimatedPriceMin;

    /// <summary>
    /// Estimated maximum market price.
    /// </summary>
    public int? EstimatedPriceMax;

    /// <summary>
    /// Whether this entry has been decrypted to show exact numbers.
    /// </summary>
    public bool IsDecrypted;

    public AppraiserArchiveEntry(
        string itemName,
        string itemDescription,
        ItemQualityTier quality,
        float wear,
        float effectiveMultiplier,
        string itemCategory = "Item",
        bool isFavorite = false)
    {
        ItemName = itemName;
        ItemDescription = itemDescription;
        Quality = quality;
        Wear = wear;
        EffectiveMultiplier = effectiveMultiplier;
        ItemCategory = itemCategory;
        IsFavorite = isFavorite;
    }
}

/// <summary>
/// UI state for the appraiser archive PDA cartridge.
/// Contains the list of scanned item entries.
/// </summary>
[Serializable, NetSerializable]
public sealed class AppraiserArchiveUiState : BoundUserInterfaceState
{
    public List<AppraiserArchiveEntry> Entries;

    public AppraiserArchiveUiState(List<AppraiserArchiveEntry> entries)
    {
        Entries = entries;
    }
}

/// <summary>
/// Message events for the appraiser archive PDA cartridge UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class AppraiserArchiveUiMessageEvent : CartridgeLoader.CartridgeMessageEvent
{
    public readonly AppraiserArchiveAction Action;
    public readonly int EntryIndex;

    public AppraiserArchiveUiMessageEvent(AppraiserArchiveAction action, int entryIndex = -1)
    {
        Action = action;
        EntryIndex = entryIndex;
    }
}

[Serializable, NetSerializable]
public enum AppraiserArchiveAction : byte
{
    ToggleFavorite,
    RemoveEntry,
    ClearAll,
    DecryptEntry,
}

/// <summary>
/// Sent from client to server when the player completes the appraiser lens QTE minigame.
/// The server validates and records the scanned item data to the PDA archive.
/// </summary>
[Serializable, NetSerializable]
public sealed class AppraiserScanRequestEvent : EntityEventArgs
{
    public readonly NetEntity Target;

    public AppraiserScanRequestEvent(NetEntity target)
    {
        Target = target;
    }
}
