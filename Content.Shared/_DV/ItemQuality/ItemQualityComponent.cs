using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.ItemQuality;

/// <summary>
/// Adds quality and wear properties to equipment (weapons, armor, hardsuits).
/// Both quality and wear affect item stats (damage, protection) and price.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ItemQualityComponent : Component
{
    /// <summary>
    /// The quality tier of this item. Randomized on spawn.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ItemQualityTier Quality = ItemQualityTier.Normal;

    /// <summary>
    /// Current wear level from 0.0 (destroyed) to 1.0 (brand new).
    /// Randomized on spawn within configured bounds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Wear = 1.0f;

    /// <summary>
    /// Minimum wear value when randomly initialized.
    /// </summary>
    [DataField]
    public float MinInitialWear = 0.25f;

    /// <summary>
    /// Maximum wear value when randomly initialized.
    /// </summary>
    [DataField]
    public float MaxInitialWear = 0.80f;

    /// <summary>
    /// Whether quality was already initialized (to prevent re-rolling on re-anchor etc).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool QualityInitialized;

    /// <summary>
    /// Multiplier for how much quality affects stats. 1.0 = default scaling.
    /// </summary>
    [DataField]
    public float QualityEffectStrength = 1.0f;

    /// <summary>
    /// Multiplier for how much wear affects stats. 1.0 = default scaling.
    /// </summary>
    [DataField]
    public float WearEffectStrength = 1.0f;

    // ─── Wear degradation settings ───

    /// <summary>
    /// How much wear is lost per melee hit (raised on weapon).
    /// Default: 0.001 → ~1000 hits to go from 1.0 to 0.0.
    /// </summary>
    [DataField]
    public float WearPerMeleeHit = 0.001f;

    /// <summary>
    /// How much wear is lost per gun shot.
    /// Default: 0.0005 → ~2000 shots to go from 1.0 to 0.0.
    /// </summary>
    [DataField]
    public float WearPerShot = 0.0005f;

    /// <summary>
    /// How much wear is lost per point of damage absorbed by armor.
    /// Default: 0.00015 → absorbing ~6666 total damage to fully degrade.
    /// Clothing/hardsuits use lower values.
    /// </summary>
    [DataField]
    public float WearPerDamageAbsorbed = 0.00015f;

    /// <summary>
    /// How much wear is lost per second while worn (clothing/armor/hardsuit).
    /// Default: ~0.0000035 → roughly 0.1 wear lost per 8-hour shift.
    /// </summary>
    [DataField]
    public float WearPerSecondWorn = 0.0000035f;

    /// <summary>
    /// Whether this item is currently being worn by someone (tracked for passive wear).
    /// </summary>
    [AutoNetworkedField]
    public bool IsWorn;

    /// <summary>
    /// Accumulated fractional time for passive wear (avoids rounding every tick).
    /// </summary>
    public float PassiveWearAccumulator;

    // ─── Weapon jamming settings ───

    /// <summary>
    /// Whether this item can jam (only applies to guns).
    /// </summary>
    [DataField]
    public bool CanJam = true;

    /// <summary>
    /// Base jam chance per shot at wear=0, quality=Junk. Scales down with better wear/quality.
    /// </summary>
    [DataField]
    public float BaseJamChance = 0.15f;

    /// <summary>
    /// Whether the weapon is currently jammed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsJammed;

    /// <summary>
    /// Sound played when the weapon jams.
    /// </summary>
    [DataField]
    public SoundSpecifier JamSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");

    // ─── Accelerated degradation settings ───

    /// <summary>
    /// Wear threshold below which accelerated degradation kicks in.
    /// At low condition, each use costs more wear, creating a death spiral.
    /// Default 0.35 = 35% condition.
    /// </summary>
    [DataField]
    public float AcceleratedWearThreshold = 0.35f;

    /// <summary>
    /// Wear multiplier applied when condition is below AcceleratedWearThreshold.
    /// Default 3.0 = 3x faster degradation at low condition.
    /// </summary>
    [DataField]
    public float AcceleratedWearMultiplier = 3.0f;

    // ─── Armor baseline storage ───

    /// <summary>
    /// Baseline armor coefficients saved on init to allow quality/wear scaling.
    /// Key = damage type, Value = original coefficient.
    /// </summary>
    public Dictionary<string, float>? BaseArmorCoefficients;

    /// <summary>
    /// Baseline armor flat reductions saved on init.
    /// </summary>
    public Dictionary<string, float>? BaseArmorFlatReduction;

    // ─── Weapon passive shield settings ───

    /// <summary>
    /// Whether this weapon can passively block damage when held in hand.
    /// Weapons absorb a fraction of incoming damage aimed at arms/torso,
    /// degrading their own wear in the process.
    /// </summary>
    [DataField]
    public bool CanShieldWhenHeld = true;

    /// <summary>
    /// Fraction of incoming damage blocked by the held weapon (0.0–1.0).
    /// Default: 0.15 = blocks 15% of incoming damage.
    /// </summary>
    [DataField]
    public float HeldShieldFraction = 0.15f;

    /// <summary>
    /// How much wear is lost per point of damage absorbed by the weapon shield.
    /// Default: 0.0003 → absorbing ~3333 total damage to fully degrade.
    /// </summary>
    [DataField]
    public float WearPerShieldDamage = 0.0003f;

    // ─── Destruction settings ───

    /// <summary>
    /// Whether this weapon can explode when wear reaches critical levels.
    /// Only applies to guns.
    /// </summary>
    [DataField]
    public bool CanExplodeOnWear;

    /// <summary>
    /// Wear threshold at which the item can randomly explode or break on use (0 = never, 0.05 = at 5% wear).
    /// </summary>
    [DataField]
    public float ExplodeAtWear = 0.05f;

    /// <summary>
    /// Explosion chance per shot when wear is at or below ExplodeAtWear.
    /// Scales with how far below the threshold the weapon is.
    /// </summary>
    [DataField]
    public float ExplodeChance = 0.1f;

    /// <summary>
    /// Chance for a weapon explosion to sever the holder's hand or arm.
    /// </summary>
    [DataField]
    public float ExplosionLimbLossChance = 0.5f;

    /// <summary>
    /// Whether the item can break into scrap at low wear.
    /// </summary>
    [DataField]
    public bool CanDestroyFromWear = true;

    /// <summary>
    /// Wear threshold below which the item may randomly break apart (0.15 = 15%).
    /// </summary>
    [DataField]
    public float DestroyWearThreshold = 0.15f;

    /// <summary>
    /// Chance per wear tick to break apart when below DestroyWearThreshold.
    /// Scales linearly: full chance at wear=0, zero chance at threshold.
    /// </summary>
    [DataField]
    public float DestroyChance = 0.08f;

    /// <summary>
    /// Entity prototypes spawned when the item is destroyed by wear.
    /// Key = prototype ID, Value = amount.
    /// </summary>
    [DataField]
    public Dictionary<string, int> DestructionLoot = new();

    /// <summary>
    /// Sound played when the item breaks from wear.
    /// </summary>
    [DataField]
    public SoundSpecifier BreakSound = new SoundPathSpecifier("/Audio/Effects/metal_break1.ogg");
}

/// <summary>
/// Quality tiers for items. Each tier provides a stat multiplier.
/// </summary>
[Serializable, NetSerializable]
public enum ItemQualityTier : byte
{
    /// <summary>
    /// Хлам — very poor quality, significant stat penalties.
    /// </summary>
    Junk = 0,

    /// <summary>
    /// Плохое — below average quality.
    /// </summary>
    Poor = 1,

    /// <summary>
    /// Обычное — average quality, no bonuses or penalties.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Хорошее — above average quality.
    /// </summary>
    Good = 3,

    /// <summary>
    /// Отличное — excellent quality, significant stat bonuses.
    /// </summary>
    Excellent = 4,

    /// <summary>
    /// Шедевр — masterwork quality, best possible stats.
    /// </summary>
    Masterwork = 5,
}
