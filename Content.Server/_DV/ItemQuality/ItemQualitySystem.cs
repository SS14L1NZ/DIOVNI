using Content.Server.Cargo.Systems;
using Content.Server.CartridgeLoader;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._DV.ItemQuality;
using Content.Shared._DV.Clothing.Events;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Armor;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared.CartridgeLoader;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._DV.ItemQuality;

/// <summary>
/// Server-side system for item quality and wear.
/// Handles random initialization on MapInit, price modification,
/// melee damage modification, gun accuracy modification,
/// gradual wear on use, weapon jamming, accelerated degradation at low condition,
/// armor coefficient scaling, item destruction at low wear,
/// weapon explosion at critical wear with limb severing.
/// </summary>
public sealed class ItemQualitySystem : SharedItemQualitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;

    /// <summary>
    /// Quality tier weights for random generation.
    /// Higher weight = more likely to appear.
    /// Most items spawn as junk or poor quality; good+ items are rare.
    /// </summary>
    private static readonly (ItemQualityTier Tier, float Weight)[] QualityWeights =
    {
        (ItemQualityTier.Junk, 25f),
        (ItemQualityTier.Poor, 35f),
        (ItemQualityTier.Normal, 25f),
        (ItemQualityTier.Good, 10f),
        (ItemQualityTier.Excellent, 4f),
        (ItemQualityTier.Masterwork, 1f),
    };

    private static readonly float TotalWeight;

    static ItemQualitySystem()
    {
        var total = 0f;
        foreach (var (_, weight) in QualityWeights)
            total += weight;
        TotalWeight = total;
    }

    /// <summary>
    /// Passive wear update interval in seconds (every 30 seconds to save performance).
    /// </summary>
    private const float PassiveWearInterval = 30f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemQualityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ItemQualityComponent, PriceCalculationEvent>(OnPriceCalculation);
        SubscribeLocalEvent<ItemQualityComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);

        // Wear-on-use events
        SubscribeLocalEvent<ItemQualityComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ItemQualityComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<ItemQualityComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnArmorDamageForWear);

        // Equipped/Unequipped tracking for passive wear
        SubscribeLocalEvent<ItemQualityComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ItemQualityComponent, GotUnequippedEvent>(OnUnequipped);

        // Unjam via activation (E key) or use in hand (Z key)
        SubscribeLocalEvent<ItemQualityComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<ItemQualityComponent, UseInHandEvent>(OnUseInHand);

        // Weapon passive shield — track hand pickup/drop
        SubscribeLocalEvent<ItemQualityComponent, GotEquippedHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<ItemQualityComponent, GotUnequippedHandEvent>(OnHandUnequipped);

        // Weapon passive shield — intercept damage on the holder
        SubscribeLocalEvent<WeaponShieldUserComponent, DamageModifyEvent>(OnWeaponShieldDamage);

        // Appraiser scan request — client sends this after QTE completion
        SubscribeNetworkEvent<AppraiserScanRequestEvent>(OnScanRequest);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Process passive wear for worn items
        var query = EntityQueryEnumerator<ItemQualityComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsWorn || comp.WearPerSecondWorn <= 0f)
                continue;

            comp.PassiveWearAccumulator += frameTime;

            if (comp.PassiveWearAccumulator < PassiveWearInterval)
                continue;

            var wearLoss = comp.WearPerSecondWorn * comp.PassiveWearAccumulator;
            comp.PassiveWearAccumulator = 0f;

            ApplyWear(uid, comp, wearLoss);
        }
    }

    // ─── Archive recording via network event ───

    /// <summary>
    /// When the client completes the appraiser lens QTE minigame, validate and record the scan to PDA archive.
    /// This replaces the old examine-based recording — stats are now only accessible via the PDA cartridge.
    /// </summary>
    private void OnScanRequest(AppraiserScanRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var target = GetEntity(msg.Target);

        if (!TryComp<ItemQualityComponent>(target, out var component))
            return;

        // Validate: user must have appraiser glasses OR be an admin ghost
        var isAdminGhost = HasComp<GhostComponent>(user) && _admin.IsAdmin(user);

        if (!isAdminGhost)
        {
            if (!_inventory.TryGetSlotEntity(user, "eyes", out var eyes) ||
                !HasComp<ItemAppraiserComponent>(eyes))
                return;
        }

        // Validate: user must have a PDA (admin ghosts skip this)
        EntityUid loaderUid = default;
        var hasPda = TryFindCartridgeLoader(user, out loaderUid);

        if (!isAdminGhost && !hasPda)
            return;

        // Range check — must be close enough to examine (admin ghosts skip)
        if (!isAdminGhost && !_transform.InRange(Transform(user).Coordinates, Transform(target).Coordinates, 3.5f))
            return;

        var meta = MetaData(target);
        var itemName = meta.EntityName;
        var itemDesc = meta.EntityDescription;
        var effectiveMul = GetEffectiveMultiplier(component);

        // Determine category
        var isGun = HasComp<GunComponent>(target);
        var isMelee = HasComp<MeleeWeaponComponent>(target);
        var isArmor = HasComp<ArmorComponent>(target);
        string category;
        if (isGun && isMelee)
            category = "WeaponHybrid";
        else if (isGun)
            category = "WeaponRanged";
        else if (isMelee)
            category = "WeaponMelee";
        else if (isArmor)
            category = "Armor";
        else
            category = "Item";

        var entry = new AppraiserArchiveEntry(itemName, itemDesc, component.Quality, component.Wear, effectiveMul, category);

        // Quality-specific data
        entry.CanJam = component.CanJam;
        entry.IsJammed = component.IsJammed;

        // Populate melee data (including per-type breakdown)
        if (isMelee && TryComp<MeleeWeaponComponent>(target, out var melee))
        {
            var totalDmg = (float) melee.Damage.GetTotal();
            entry.MeleeDamage = MathF.Round(totalDmg);
            entry.AdjustedMeleeDamage = MathF.Round(totalDmg * effectiveMul);

            // Per-type damage breakdown
            if (melee.Damage.DamageDict.Count > 0)
            {
                entry.MeleeDamageTypes = new Dictionary<string, float>(melee.Damage.DamageDict.Count);
                foreach (var (type, value) in melee.Damage.DamageDict)
                {
                    var dmg = (float) value;
                    if (dmg > 0)
                        entry.MeleeDamageTypes[type] = MathF.Round(dmg * effectiveMul);
                }
            }
        }

        // Populate gun data (expanded with spread, speed, fire mode)
        if (isGun && TryComp<GunComponent>(target, out var gun))
        {
            entry.FireRate = gun.FireRateModified > 0 ? MathF.Round(1f / gun.FireRateModified) : 0;
            entry.GunMinAngle = MathF.Round((float) gun.MinAngleModified.Degrees);
            entry.GunMaxAngle = MathF.Round((float) gun.MaxAngleModified.Degrees);
            entry.ProjectileSpeed = MathF.Round(gun.ProjectileSpeedModified);
            entry.GunFireMode = gun.SelectedMode.ToString();
            if ((gun.AvailableModes & SelectiveFire.Burst) != 0)
                entry.ShotsPerBurst = gun.ShotsPerBurstModified;
        }

        // Populate armor data
        if (isArmor && TryComp<ArmorComponent>(target, out var armor))
        {
            if (armor.Modifiers.Coefficients.Count > 0)
                entry.ArmorCoefficients = new Dictionary<string, float>(armor.Modifiers.Coefficients);
            if (armor.Modifiers.FlatReduction.Count > 0)
                entry.ArmorFlatReduction = new Dictionary<string, float>(armor.Modifiers.FlatReduction);
        }

        // Populate speed modifier data
        if (TryComp<ClothingSpeedModifierComponent>(target, out var speedMod))
        {
            var walkSlow = MathF.Round((1.0f - speedMod.WalkModifier) * 100f, 1);
            var sprintSlow = MathF.Round((1.0f - speedMod.SprintModifier) * 100f, 1);
            var maxEffect = Math.Max(MathF.Abs(walkSlow), MathF.Abs(sprintSlow));
            if (maxEffect > 0)
            {
                // Positive = slowdown, negative = speed boost
                entry.SpeedModifierPercent = walkSlow > 0 || sprintSlow > 0 ? maxEffect : -maxEffect;
            }
        }

        // Populate explosion resistance data
        if (TryComp<ExplosionResistanceComponent>(target, out var explosionRes))
        {
            var explResValue = MathF.Round((1f - explosionRes.DamageCoefficient) * 100, 1);
            if (explResValue > 0)
                entry.ExplosionResistance = explResValue;
        }

        // Populate stamina resistance data
        if (TryComp<StaminaDamageResistanceComponent>(target, out var staminaRes))
        {
            var stamResValue = MathF.Round((1f - staminaRes.Coefficient) * 100, 1);
            if (stamResValue > 0)
                entry.StaminaResistance = stamResValue;
        }

        // Populate estimated price range (requires AppraisalCartridge in PDA)
        var hasPriceModule = hasPda &&
            _cartridgeLoader.TryGetProgram<AppraisalCartridgeComponent>(loaderUid, out _, out _);

        if (isAdminGhost || hasPriceModule)
        {
            var currentPrice = _pricing.GetPrice(target);
            if (currentPrice > 0)
            {
                // Price varies ±20% around current value to simulate market fluctuation
                var minPrice = (int) Math.Round(currentPrice * 0.8);
                var maxPrice = (int) Math.Round(currentPrice * 1.2);
                entry.EstimatedPriceMin = Math.Max(1, minPrice);
                entry.EstimatedPriceMax = Math.Max(1, maxPrice);
            }
        }

        // Admin ghosts without a PDA — skip archive recording, the examine verb already shows stats
        if (!hasPda)
            return;

        var archiveSystem = EntityManager.System<AppraiserArchiveCartridgeSystem>();
        archiveSystem.AddScanEntry(loaderUid, entry);
    }

    /// <summary>
    /// Try to find a CartridgeLoader (PDA) on the user.
    /// </summary>
    private bool TryFindCartridgeLoader(EntityUid user, out EntityUid loaderUid)
    {
        loaderUid = default;

        // Check PDA slot (idcard slot usually has a PDA)
        if (_inventory.TryGetSlotEntity(user, "id", out var idItem) &&
            HasComp<CartridgeLoaderComponent>(idItem))
        {
            loaderUid = idItem.Value;
            return true;
        }

        // Check belt slot
        if (_inventory.TryGetSlotEntity(user, "belt", out var beltItem) &&
            HasComp<CartridgeLoaderComponent>(beltItem))
        {
            loaderUid = beltItem.Value;
            return true;
        }

        return false;
    }

    // ─── Map initialization ───

    private void OnMapInit(EntityUid uid, ItemQualityComponent component, MapInitEvent args)
    {
        if (component.QualityInitialized)
            return;

        component.QualityInitialized = true;

        // Random quality based on weighted distribution
        component.Quality = PickRandomQuality();

        // Random wear between configured min and max
        component.Wear = _random.NextFloat(component.MinInitialWear, component.MaxInitialWear);

        // Hide base gun examine text — weapon stats are only accessible via appraiser glasses + PDA archive
        if (TryComp<GunComponent>(uid, out var gun))
            gun.ShowExamineText = false;

        // Save baseline armor data and apply initial quality scaling
        SaveArmorBaseline(uid, component);
        UpdateArmorModifiers(uid, component);

        Dirty(uid, component);
    }

    private ItemQualityTier PickRandomQuality()
    {
        var roll = _random.NextFloat(0f, TotalWeight);
        var cumulative = 0f;

        foreach (var (tier, weight) in QualityWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return tier;
        }

        return ItemQualityTier.Normal;
    }

    // ─── Armor coefficient modification ───

    /// <summary>
    /// Saves the original armor coefficients and flat reductions
    /// so we can scale them with quality/wear.
    /// </summary>
    private void SaveArmorBaseline(EntityUid uid, ItemQualityComponent component)
    {
        if (!TryComp<ArmorComponent>(uid, out var armor))
            return;

        component.BaseArmorCoefficients = new Dictionary<string, float>(armor.Modifiers.Coefficients);
        component.BaseArmorFlatReduction = new Dictionary<string, float>(armor.Modifiers.FlatReduction);
    }

    /// <summary>
    /// Updates ArmorComponent.Modifiers based on current quality and wear.
    /// Protection is scaled: newCoeff = 1 - (1 - baseCoeff) * multiplier.
    /// At multiplier=1.0: normal protection. At 0.0: no protection. At 1.5: 50% better.
    /// </summary>
    private void UpdateArmorModifiers(EntityUid uid, ItemQualityComponent component)
    {
        if (!TryComp<ArmorComponent>(uid, out var armor))
            return;

        if (component.BaseArmorCoefficients == null || component.BaseArmorFlatReduction == null)
            return;

        var multiplier = GetEffectiveMultiplier(component);

        // Scale coefficients: protection = (1 - coeff), scaled protection = protection * multiplier
        foreach (var (key, baseCoeff) in component.BaseArmorCoefficients)
        {
            var protection = 1f - baseCoeff; // e.g., 0.3 for coeff 0.7
            var scaledProtection = protection * multiplier;
            // Clamp so coefficient stays in [0, 1] range
            armor.Modifiers.Coefficients[key] = MathHelper.Clamp(1f - scaledProtection, 0f, 1f);
        }

        // Scale flat reductions linearly
        foreach (var (key, baseFlat) in component.BaseArmorFlatReduction)
        {
            armor.Modifiers.FlatReduction[key] = MathF.Max(0f, baseFlat * multiplier);
        }

        Dirty(uid, armor);
    }

    // ─── Stat modification ───

    /// <summary>
    /// Modifies item price based on quality and wear.
    /// </summary>
    private void OnPriceCalculation(EntityUid uid, ItemQualityComponent component, ref PriceCalculationEvent args)
    {
        var multiplier = GetEffectiveMultiplier(component);
        args.Price *= multiplier;
    }

    /// <summary>
    /// Modifies melee weapon damage based on quality and wear.
    /// </summary>
    private void OnGetMeleeDamage(EntityUid uid, ItemQualityComponent component, ref GetMeleeDamageEvent args)
    {
        var multiplier = GetEffectiveMultiplier(component);
        args.Damage *= multiplier;
    }

    // ─── Wear on use ───

    /// <summary>
    /// Degrade melee weapon on hit.
    /// </summary>
    private void OnMeleeHit(EntityUid uid, ItemQualityComponent component, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        ApplyWear(uid, component, component.WearPerMeleeHit);
    }

    /// <summary>
    /// Degrade gun on shot, handle accelerated degradation at low condition,
    /// jamming and critical-wear explosion.
    /// </summary>
    private void OnAmmoShot(EntityUid uid, ItemQualityComponent component, AmmoShotEvent args)
    {
        var wearAmount = component.WearPerShot;

        // Accelerated degradation: at low wear, each shot costs more
        if (component.Wear < component.AcceleratedWearThreshold)
            wearAmount *= component.AcceleratedWearMultiplier;

        ApplyWear(uid, component, wearAmount);

        // Check for critical-wear explosion (before jamming, since explosion destroys the weapon)
        if (component.CanExplodeOnWear && component.Wear <= component.ExplodeAtWear && HasComp<GunComponent>(uid))
        {
            // Scale explosion chance: ExplodeChance at threshold, 100% at wear=0
            var wearRatio = component.ExplodeAtWear > 0f ? component.Wear / component.ExplodeAtWear : 1f;
            var explodeChance = component.ExplodeChance + (1f - component.ExplodeChance) * (1f - wearRatio);

            if (_random.NextFloat() < explodeChance)
            {
                ExplodeWeapon(uid, component);
                return; // weapon is destroyed, stop processing
            }
        }

        // Check for jamming after the shot
        if (component.CanJam && !component.IsJammed && HasComp<GunComponent>(uid))
        {
            var jamChance = CalculateJamChance(component);
            if (_random.NextFloat() < jamChance)
            {
                JamWeapon(uid, component);
            }
        }
    }

    /// <summary>
    /// Degrade armor/clothing based on incoming damage (via inventory relay).
    /// </summary>
    private void OnArmorDamageForWear(EntityUid uid, ItemQualityComponent component,
        InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (component.WearPerDamageAbsorbed <= 0f)
            return;

        var totalIncoming = (float) args.Args.Damage.GetTotal();
        if (totalIncoming > 0)
        {
            ApplyWear(uid, component, component.WearPerDamageAbsorbed * totalIncoming);
        }
    }

    // ─── Equipped tracking for passive wear ───

    private void OnEquipped(EntityUid uid, ItemQualityComponent component, GotEquippedEvent args)
    {
        component.IsWorn = true;
        component.PassiveWearAccumulator = 0f;
    }

    private void OnUnequipped(EntityUid uid, ItemQualityComponent component, GotUnequippedEvent args)
    {
        component.IsWorn = false;
        component.PassiveWearAccumulator = 0f;
    }

    // ─── Weapon passive shield (held in hand) ───

    /// <summary>
    /// When a quality weapon enters a hand, add the shield marker to the holder.
    /// </summary>
    private void OnHandEquipped(EntityUid uid, ItemQualityComponent component, GotEquippedHandEvent args)
    {
        if (!component.CanShieldWhenHeld)
            return;

        // Only weapons (gun or melee) can shield
        if (!HasComp<GunComponent>(uid) && !HasComp<MeleeWeaponComponent>(uid))
            return;

        EnsureComp<WeaponShieldUserComponent>(args.User);
    }

    /// <summary>
    /// When a quality weapon leaves a hand, remove the shield marker if no other shield weapons remain.
    /// </summary>
    private void OnHandUnequipped(EntityUid uid, ItemQualityComponent component, GotUnequippedHandEvent args)
    {
        if (!component.CanShieldWhenHeld)
            return;

        // Check if the user still has any other shield-capable weapons in their hands
        var hasOtherShield = false;
        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (held == uid)
                continue;
            if (TryComp<ItemQualityComponent>(held, out var otherQuality) &&
                otherQuality.CanShieldWhenHeld &&
                (HasComp<GunComponent>(held) || HasComp<MeleeWeaponComponent>(held)))
            {
                hasOtherShield = true;
                break;
            }
        }

        if (!hasOtherShield)
            RemCompDeferred<WeaponShieldUserComponent>(args.User);
    }

    /// <summary>
    /// When a mob with a held weapon takes damage, the weapon absorbs a fraction as wear.
    /// </summary>
    private void OnWeaponShieldDamage(EntityUid uid, WeaponShieldUserComponent component, DamageModifyEvent args)
    {
        var totalDamage = (float) args.Damage.GetTotal();
        if (totalDamage <= 0)
            return;

        // Find the best shield weapon in hands
        EntityUid? bestWeapon = null;
        ItemQualityComponent? bestQuality = null;

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!TryComp<ItemQualityComponent>(held, out var quality))
                continue;
            if (!quality.CanShieldWhenHeld)
                continue;
            if (!HasComp<GunComponent>(held) && !HasComp<MeleeWeaponComponent>(held))
                continue;
            if (quality.Wear <= 0.01f)
                continue; // nearly broken weapons can't shield

            if (bestQuality == null || quality.Wear > bestQuality.Wear)
            {
                bestWeapon = held;
                bestQuality = quality;
            }
        }

        if (bestWeapon == null || bestQuality == null)
            return;

        var blockFraction = bestQuality.HeldShieldFraction;

        // Scale block fraction by weapon condition — damaged weapons block less
        blockFraction *= bestQuality.Wear;

        // Reduce incoming damage
        args.Damage *= 1f - blockFraction;

        // Apply wear to the weapon based on absorbed damage
        var absorbedDamage = totalDamage * blockFraction;
        ApplyWear(bestWeapon.Value, bestQuality, bestQuality.WearPerShieldDamage * absorbedDamage);
    }

    // ─── Weapon jamming ───

    /// <summary>
    /// Calculate jam chance based on quality and wear.
    /// </summary>
    private float CalculateJamChance(ItemQualityComponent component)
    {
        var qualityFactor = component.Quality switch
        {
            ItemQualityTier.Junk => 1.0f,
            ItemQualityTier.Poor => 0.6f,
            ItemQualityTier.Normal => 0.2f,
            ItemQualityTier.Good => 0.05f,
            ItemQualityTier.Excellent => 0.01f,
            ItemQualityTier.Masterwork => 0.0f,
            _ => 0.2f,
        };

        // Wear factor: 1.0 at wear=0, 0.0 at wear=1.0
        var wearFactor = 1.0f - component.Wear;

        return component.BaseJamChance * qualityFactor * wearFactor;
    }

    /// <summary>
    /// Jam the weapon — prevent further firing until unjammed.
    /// </summary>
    private void JamWeapon(EntityUid uid, ItemQualityComponent component)
    {
        component.IsJammed = true;
        Dirty(uid, component);

        _audio.PlayPvs(component.JamSound, uid);
        _popup.PopupEntity(Loc.GetString("item-quality-weapon-jammed"), uid, PopupType.MediumCaution);
    }

    /// <summary>
    /// Unjam the weapon (called via interaction).
    /// </summary>
    public void UnjamWeapon(EntityUid uid, ItemQualityComponent component)
    {
        component.IsJammed = false;
        Dirty(uid, component);
        _popup.PopupEntity(Loc.GetString("item-quality-weapon-unjammed"), uid, PopupType.Small);
    }

    /// <summary>
    /// Unjam the weapon when activated (E key) while jammed.
    /// </summary>
    private void OnActivateInWorld(EntityUid uid, ItemQualityComponent component, ActivateInWorldEvent args)
    {
        if (!component.IsJammed)
            return;

        args.Handled = true;
        UnjamWeapon(uid, component);
    }

    /// <summary>
    /// Unjam the weapon when used in hand (Z key) while jammed.
    /// </summary>
    private void OnUseInHand(EntityUid uid, ItemQualityComponent component, UseInHandEvent args)
    {
        if (!component.IsJammed)
            return;

        args.Handled = true;
        UnjamWeapon(uid, component);
    }

    // ─── Destruction ───

    /// <summary>
    /// Triggers a small explosion at the weapon's location, then destroys it.
    /// With a chance to sever the holder's hand.
    /// </summary>
    private void ExplodeWeapon(EntityUid uid, ItemQualityComponent component)
    {
        _popup.PopupEntity(Loc.GetString("item-quality-weapon-exploded"), uid, PopupType.LargeCaution);

        // Play audio at fixed coordinates — the entity is about to be deleted,
        // so anchoring the audio to it would cause NaN position in the client audio system.
        var coords = Transform(uid).Coordinates;
        _audio.PlayPvs(component.BreakSound, coords);

        // Find the holder (parent in the transform tree, typically the character)
        var holder = Transform(uid).ParentUid;

        // Queue a small fire-type explosion (intensity similar to a molotov)
        _explosion.QueueExplosion(uid, "FireBomb", 4f, 1f, 3f, canCreateVacuum: false);

        // Spawn destruction loot on the ground at the holder's position
        SpawnDestructionLoot(uid, component);

        // Chance to sever a hand on the holder
        if (holder.IsValid() && _random.NextFloat() < component.ExplosionLimbLossChance)
        {
            TrySeverHand(holder);
        }

        // Destroy the weapon
        QueueDel(uid);
    }

    /// <summary>
    /// Attempts to sever a random hand from the target entity.
    /// Falls back to arm if no hands are available.
    /// </summary>
    private void TrySeverHand(EntityUid target)
    {
        if (!TryComp<BodyComponent>(target, out var body))
            return;

        // Try to find a random hand, then fall back to arms — avoid ToList() allocations
        EntityUid? picked = null;
        var count = 0;

        foreach (var part in _body.GetBodyChildrenOfType(target, BodyPartType.Hand, body))
        {
            count++;
            // Reservoir sampling: pick each element with probability 1/count
            if (_random.Next(count) == 0)
                picked = part.Id;
        }

        if (picked == null)
        {
            count = 0;
            foreach (var part in _body.GetBodyChildrenOfType(target, BodyPartType.Arm, body))
            {
                count++;
                if (_random.Next(count) == 0)
                    picked = part.Id;
            }
        }

        if (picked != null)
        {
            var ev = new AmputateAttemptEvent(picked.Value);
            RaiseLocalEvent(picked.Value, ref ev);
            _popup.PopupEntity(Loc.GetString("item-quality-weapon-exploded-limb"), target, PopupType.LargeCaution);
        }
    }

    /// <summary>
    /// Destroys an item that reached low wear, spawning appropriate scrap.
    /// </summary>
    private void DestroyFromWear(EntityUid uid, ItemQualityComponent component)
    {
        _popup.PopupEntity(Loc.GetString("item-quality-item-destroyed"), uid, PopupType.MediumCaution);

        // Play audio at fixed coordinates — the entity is about to be deleted.
        var coords = Transform(uid).Coordinates;
        _audio.PlayPvs(component.BreakSound, coords);

        SpawnDestructionLoot(uid, component);
        QueueDel(uid);
    }

    /// <summary>
    /// Spawns the configured destruction loot on the ground.
    /// Uses the mover coordinates so loot drops at map position, not on the character.
    /// </summary>
    private void SpawnDestructionLoot(EntityUid uid, ItemQualityComponent component)
    {
        if (component.DestructionLoot.Count == 0)
            return;

        // Get the map-level coordinates so loot drops on the ground
        var coords = _transform.GetMoverCoordinates(uid);
        foreach (var (protoId, amount) in component.DestructionLoot)
        {
            for (var i = 0; i < amount; i++)
            {
                Spawn(protoId, coords);
            }
        }
    }

    // ─── Helpers ───

    /// <summary>
    /// Applies wear degradation, clamping to [0, 1].
    /// Updates armor modifiers and checks for destruction.
    /// </summary>
    private void ApplyWear(EntityUid uid, ItemQualityComponent component, float amount)
    {
        if (amount <= 0f)
            return;

        var oldWear = component.Wear;
        component.Wear = MathF.Max(0f, component.Wear - amount);

        if (!MathHelper.CloseTo(oldWear, component.Wear, 0.0001f))
        {
            Dirty(uid, component);

            // Refresh gun modifiers if this is a gun so spread updates live
            if (HasComp<GunComponent>(uid))
                _gun.RefreshModifiers(uid);

            // Update armor coefficients to reflect new wear
            UpdateArmorModifiers(uid, component);

            // Check for destruction at low wear
            if (component.CanDestroyFromWear && component.Wear <= component.DestroyWearThreshold)
            {
                // Scale chance: 0% at threshold, full DestroyChance at wear=0
                var ratio = component.DestroyWearThreshold > 0f
                    ? 1f - component.Wear / component.DestroyWearThreshold
                    : 1f;
                if (_random.NextFloat() < component.DestroyChance * ratio)
                {
                    DestroyFromWear(uid, component);
                }
            }
        }
    }

    /// <summary>
    /// Public method to restore wear (used by the repair system).
    /// Updates armor modifiers and gun spread after repair.
    /// </summary>
    public void RepairWear(EntityUid uid, ItemQualityComponent component, float amount)
    {
        component.Wear = MathF.Min(1.0f, component.Wear + amount);
        Dirty(uid, component);

        if (HasComp<GunComponent>(uid))
            _gun.RefreshModifiers(uid);

        UpdateArmorModifiers(uid, component);
    }
}
