using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Utility;

namespace Content.Shared._DV.ItemQuality;

/// <summary>
/// Shared system for item quality and wear. Handles examine verbs and stat modification.
/// Quality and wear together produce an effective multiplier that scales armor coefficients,
/// melee damage, and item prices.
/// </summary>
public abstract class SharedItemQualitySystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemQualityComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ItemQualityComponent, GetVerbsEvent<ExamineVerb>>(OnExamineVerb);
        SubscribeLocalEvent<ItemQualityComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<ItemQualityComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    /// <summary>
    /// Gets the stat multiplier from quality tier.
    /// Normal = 1.0, Junk = 0.5, Masterwork = 1.5
    /// </summary>
    public static float GetQualityMultiplier(ItemQualityTier quality)
    {
        return quality switch
        {
            ItemQualityTier.Junk => 0.50f,
            ItemQualityTier.Poor => 0.75f,
            ItemQualityTier.Normal => 1.00f,
            ItemQualityTier.Good => 1.15f,
            ItemQualityTier.Excellent => 1.30f,
            ItemQualityTier.Masterwork => 1.50f,
            _ => 1.00f,
        };
    }

    /// <summary>
    /// Gets the combined effective multiplier (quality * wear).
    /// </summary>
    public static float GetEffectiveMultiplier(ItemQualityComponent comp)
    {
        var qualityMul = GetQualityMultiplier(comp.Quality);
        // Quality effect: lerp from 1.0 toward qualityMul based on strength setting
        var effectiveQuality = 1.0f + (qualityMul - 1.0f) * comp.QualityEffectStrength;
        // Wear effect: lerp from 1.0 toward wear based on strength setting
        var effectiveWear = 1.0f + (comp.Wear - 1.0f) * comp.WearEffectStrength;
        return effectiveQuality * effectiveWear;
    }

    /// <summary>
    /// Returns the wear category string key for the current wear level.
    /// </summary>
    public static string GetWearCategory(float wear)
    {
        return wear switch
        {
            >= 0.9f => "item-quality-wear-pristine",
            >= 0.7f => "item-quality-wear-good",
            >= 0.5f => "item-quality-wear-worn",
            >= 0.3f => "item-quality-wear-damaged",
            _ => "item-quality-wear-broken",
        };
    }

    /// <summary>
    /// Returns the color markup for a quality tier.
    /// </summary>
    public static string GetQualityColor(ItemQualityTier quality)
    {
        return quality switch
        {
            ItemQualityTier.Junk => "#808080",       // Gray
            ItemQualityTier.Poor => "#b05050",        // Dark red
            ItemQualityTier.Normal => "#ffffff",       // White
            ItemQualityTier.Good => "#50b050",         // Green
            ItemQualityTier.Excellent => "#5050ff",    // Blue
            ItemQualityTier.Masterwork => "#ffa500",   // Orange/Gold
            _ => "#ffffff",
        };
    }

    /// <summary>
    /// Returns the color markup for a wear level.
    /// </summary>
    public static string GetWearColor(float wear)
    {
        return wear switch
        {
            >= 0.9f => "#50ff50",   // Bright green
            >= 0.7f => "#b0ff50",   // Yellow-green
            >= 0.5f => "#ffff50",   // Yellow
            >= 0.3f => "#ff8050",   // Orange
            _ => "#ff5050",         // Red
        };
    }

    /// <summary>
    /// Adds a brief quality/wear summary to the basic shift-examine text.
    /// </summary>
    protected virtual void OnExamined(EntityUid uid, ItemQualityComponent component, ExaminedEvent args)
    {
        var qualityName = Loc.GetString("item-quality-tier-" + component.Quality.ToString().ToLower());
        var qualityColor = GetQualityColor(component.Quality);
        var wearCategory = Loc.GetString(GetWearCategory(component.Wear));
        var wearColor = GetWearColor(component.Wear);

        args.PushMarkup(Loc.GetString("item-quality-examine-short",
            ("quality", $"[color={qualityColor}]{qualityName}[/color]"),
            ("condition", $"[color={wearColor}]{wearCategory}[/color]")));
    }

    /// <summary>
    /// Checks whether the examiner is allowed to see detailed quality stats.
    /// Allowed: admin ghosts, or players wearing appraiser glasses.
    /// </summary>
    private bool CanSeeDetailedStats(EntityUid examiner)
    {
        // Admin ghosts always see stats without glasses or minigame
        if (HasComp<GhostComponent>(examiner) && _admin.IsAdmin(examiner))
            return true;

        // Players need appraiser glasses equipped in the eyes slot
        if (_inventory.TryGetSlotEntity(examiner, "eyes", out var eyes) &&
            HasComp<ItemAppraiserComponent>(eyes))
            return true;

        return false;
    }

    private void OnExamineVerb(EntityUid uid, ItemQualityComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Detailed quality examine verb is only for admin ghosts and players with appraiser glasses
        if (!CanSeeDetailedStats(args.User))
            return;

        var msg = new FormattedMessage();

        // Quality line
        var qualityName = Loc.GetString("item-quality-tier-" + component.Quality.ToString().ToLower());
        var qualityColor = GetQualityColor(component.Quality);
        msg.AddMarkupOrThrow(Loc.GetString("item-quality-examine-quality",
            ("quality", $"[color={qualityColor}]{qualityName}[/color]")));

        msg.PushNewline();

        // Wear line — always word description (exact numbers only via PDA archive)
        var wearCategory = Loc.GetString(GetWearCategory(component.Wear));
        var wearColor = GetWearColor(component.Wear);
        msg.AddMarkupOrThrow(Loc.GetString("item-quality-examine-wear-vague",
            ("condition", $"[color={wearColor}]{wearCategory}[/color]")));

        msg.PushNewline();

        // Effectiveness line — always word description
        var effectiveMul = GetEffectiveMultiplier(component);
        var ratingKey = GetEffectivenessRating(effectiveMul);
        var ratingColor = effectiveMul >= 1.0f ? "#50ff50" : "#ff5050";
        msg.AddMarkupOrThrow(Loc.GetString("item-quality-examine-effectiveness-vague",
            ("rating", $"[color={ratingColor}]{Loc.GetString(ratingKey)}[/color]")));

        // Accelerated degradation warning
        if (component.Wear < component.AcceleratedWearThreshold)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("item-quality-examine-accelerated-wear"));
        }

        _examine.AddDetailedExamineVerb(args, component, msg,
            Loc.GetString("item-quality-verb-text"),
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("item-quality-verb-message"));
    }

    /// <summary>
    /// Returns a localization key for a word-based damage rating.
    /// </summary>
    public static string GetDamageRating(float damage)
    {
        return damage switch
        {
            >= 30f => "item-quality-damage-devastating",
            >= 20f => "item-quality-damage-powerful",
            >= 12f => "item-quality-damage-moderate",
            >= 6f => "item-quality-damage-light",
            _ => "item-quality-damage-negligible",
        };
    }

    /// <summary>
    /// Returns the color for a damage rating.
    /// </summary>
    public static string GetDamageColor(float damage)
    {
        return damage switch
        {
            >= 30f => "#ff3030",
            >= 20f => "#ff8050",
            >= 12f => "#ffff50",
            >= 6f => "#b0ff50",
            _ => "#808080",
        };
    }

    /// <summary>
    /// Returns a localization key for a word-based effectiveness rating.
    /// </summary>
    public static string GetEffectivenessRating(float multiplier)
    {
        return multiplier switch
        {
            >= 1.4f => "item-quality-effectiveness-outstanding",
            >= 1.2f => "item-quality-effectiveness-high",
            >= 1.0f => "item-quality-effectiveness-normal",
            >= 0.8f => "item-quality-effectiveness-reduced",
            >= 0.6f => "item-quality-effectiveness-poor",
            _ => "item-quality-effectiveness-terrible",
        };
    }

    /// <summary>
    /// Prevents jammed guns from firing (shared for client prediction).
    /// </summary>
    private void OnShotAttempted(EntityUid uid, ItemQualityComponent component, ref ShotAttemptedEvent args)
    {
        if (component.IsJammed)
            args.Cancel();
    }

    /// <summary>
    /// Modifies gun accuracy based on quality and wear (shared for client prediction parity).
    /// Worn/low quality guns have worse spread, but capped to avoid unplayable levels.
    /// </summary>
    private void OnGunRefreshModifiers(EntityUid uid, ItemQualityComponent component, ref GunRefreshModifiersEvent args)
    {
        var multiplier = GetEffectiveMultiplier(component);

        if (MathHelper.CloseTo(multiplier, 1f, 0.001f))
            return;

        // Inverse multiplier for spread — worse quality = more spread
        // Use square root to soften the effect: at 0.5x multiplier, spreadMul ≈ 1.41 instead of 2.0
        var spreadMul = 1f / MathF.Sqrt(multiplier);

        // Cap maximum spread multiplier at 2.0 (double the base spread)
        spreadMul = MathF.Min(spreadMul, 2.0f);

        args.MinAngle = Angle.FromDegrees(args.MinAngle.Degrees * spreadMul);
        args.MaxAngle = Angle.FromDegrees(args.MaxAngle.Degrees * spreadMul);
        args.AngleIncrease = Angle.FromDegrees(args.AngleIncrease.Degrees * spreadMul);
    }
}
