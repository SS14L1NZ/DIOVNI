using Content.Shared._DV.ItemQuality;
using Content.Shared.Armor;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._DV.ItemQuality;

/// <summary>
/// Handles weapon and armor repair interactions.
/// Weapon repair: use repair kit on weapon → consumes steel + plasteel + kit → restores wear.
/// Armor repair: use welder on armor → consumes plasteel → restores wear.
/// Materials can be taken from hands, inventory (including backpacks), or the ground nearby.
/// </summary>
public sealed class ItemRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly ItemQualitySystem _qualitySystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// How much wear armor repair restores (0.20 = 20%).
    /// </summary>
    private const float ArmorRepairAmount = 0.20f;

    /// <summary>
    /// Plasteel required for armor repair.
    /// </summary>
    private const int ArmorRepairPlasteel = 5;

    /// <summary>
    /// DoAfter delay for armor repair in seconds.
    /// </summary>
    private const float ArmorRepairDelay = 4f;

    /// <summary>
    /// Sound played on successful repair.
    /// </summary>
    private static readonly SoundPathSpecifier RepairSound = new("/Audio/Items/welder2.ogg");

    public override void Initialize()
    {
        base.Initialize();

        // Weapon repair: use repair kit on a weapon
        SubscribeLocalEvent<WeaponRepairKitComponent, AfterInteractEvent>(OnRepairKitUsed);
        SubscribeLocalEvent<WeaponRepairKitComponent, WeaponRepairDoAfterEvent>(OnWeaponRepairFinished);

        // Armor repair: use welder on armor
        SubscribeLocalEvent<ArmorComponent, InteractUsingEvent>(OnArmorInteractUsing);
        SubscribeLocalEvent<ArmorComponent, ArmorRepairDoAfterEvent>(OnArmorRepairFinished);
    }

    // ─── Weapon Repair ───

    private void OnRepairKitUsed(EntityUid uid, WeaponRepairKitComponent kit, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        var target = args.Target.Value;

        // Target must have ItemQualityComponent and be a weapon
        if (!TryComp<ItemQualityComponent>(target, out var quality))
            return;

        if (!HasComp<GunComponent>(target) && !HasComp<MeleeWeaponComponent>(target))
            return;

        // Don't repair if already at max
        if (quality.Wear >= 1.0f)
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-not-needed"), target, args.User);
            args.Handled = true;
            return;
        }

        // Check materials
        if (!HasRequiredMaterials(args.User, kit.RequiredSteel, kit.RequiredPlasteel, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-no-materials"), target, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        // Start doAfter
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, kit.RepairDelay,
            new WeaponRepairDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-start-weapon"), target, args.User);
        }

        args.Handled = true;
    }

    private void OnWeaponRepairFinished(EntityUid uid, WeaponRepairKitComponent kit, WeaponRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null || args.Used == null)
            return;

        var target = args.Target.Value;

        if (!TryComp<ItemQualityComponent>(target, out var quality))
            return;

        // Re-check and consume materials
        if (!HasRequiredMaterials(args.User, kit.RequiredSteel, kit.RequiredPlasteel, out var steelStacks, out var plasteelStacks))
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-no-materials"), target, args.User, PopupType.MediumCaution);
            return;
        }

        ConsumeMaterials(steelStacks, kit.RequiredSteel);
        ConsumeMaterials(plasteelStacks, kit.RequiredPlasteel);

        // Apply repair via the quality system (handles armor + gun refresh)
        _qualitySystem.RepairWear(target, quality, kit.RepairAmount);

        _audio.PlayPvs(RepairSound, target);
        _popup.PopupEntity(Loc.GetString("item-quality-repair-success-weapon"), target, args.User);

        // Consume the repair kit
        QueueDel(uid);
    }

    // ─── Armor Repair ───

    private void OnArmorInteractUsing(EntityUid uid, ArmorComponent armor, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Must be using a welder
        if (!HasComp<WelderComponent>(args.Used))
            return;

        // Target must have ItemQualityComponent (i.e. wear system)
        if (!TryComp<ItemQualityComponent>(uid, out var quality))
            return;

        // Don't repair if at max
        if (quality.Wear >= 1.0f)
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-not-needed"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Check plasteel
        if (!HasRequiredMaterials(args.User, 0, ArmorRepairPlasteel, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-no-plasteel"), uid, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        // Start doAfter
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ArmorRepairDelay,
            new ArmorRepairDoAfterEvent(), uid, target: uid, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-start-armor"), uid, args.User);
        }

        args.Handled = true;
    }

    private void OnArmorRepairFinished(EntityUid uid, ArmorComponent armor, ArmorRepairDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ItemQualityComponent>(uid, out var quality))
            return;

        // Re-check and consume plasteel
        if (!HasRequiredMaterials(args.User, 0, ArmorRepairPlasteel, out _, out var plasteelStacks))
        {
            _popup.PopupEntity(Loc.GetString("item-quality-repair-no-plasteel"), uid, args.User, PopupType.MediumCaution);
            return;
        }

        ConsumeMaterials(plasteelStacks, ArmorRepairPlasteel);

        // Apply repair via the quality system (handles armor modifier recalculation)
        _qualitySystem.RepairWear(uid, quality, ArmorRepairAmount);

        _audio.PlayPvs(RepairSound, uid);
        _popup.PopupEntity(Loc.GetString("item-quality-repair-success-armor"), uid, args.User);
    }

    // ─── Material Helpers ───

    /// <summary>
    /// Range (in tiles) to search for materials on the ground around the user.
    /// </summary>
    private const float GroundSearchRange = 1.5f;

    /// <summary>
    /// Checks if the user has enough Steel and Plasteel in hands, inventory (including backpacks), or on the ground.
    /// Collects candidate entities from the user's children, inventory containers, and nearby ground.
    /// Uses a single pass over candidates to count and collect matching stacks.
    /// </summary>
    private bool HasRequiredMaterials(EntityUid user, int steelNeeded, int plasteelNeeded,
        out List<(EntityUid Uid, StackComponent Stack)> steelStacks,
        out List<(EntityUid Uid, StackComponent Stack)> plasteelStacks)
    {
        steelStacks = new();
        plasteelStacks = new();

        // Local aliases — C# forbids capturing out parameters in local functions (CS1628)
        var localSteelStacks = steelStacks;
        var localPlasteelStacks = plasteelStacks;
        var steelFound = 0;
        var plasteelFound = 0;

        var stackQuery = GetEntityQuery<StackComponent>();

        // Helper: check a candidate entity and add to appropriate stack list
        void CheckCandidate(EntityUid candidate)
        {
            if (!stackQuery.TryGetComponent(candidate, out var stack))
                return;

            if (steelNeeded > 0 && steelFound < steelNeeded && stack.StackTypeId == "Steel")
            {
                steelFound += stack.Count;
                localSteelStacks.Add((candidate, stack));
            }
            else if (plasteelNeeded > 0 && plasteelFound < plasteelNeeded && stack.StackTypeId == "Plasteel")
            {
                plasteelFound += stack.Count;
                localPlasteelStacks.Add((candidate, stack));
            }
        }

        // 1. Direct children of the user (hands, pockets) and their containers (backpacks, bags)
        var xformQuery = GetEntityQuery<TransformComponent>();
        var childEnumerator = xformQuery.GetComponent(user).ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            CheckCandidate(child);

            foreach (var container in _container.GetAllContainers(child))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    CheckCandidate(contained);
                }
            }
        }

        // Early exit if already found enough
        if (steelFound >= steelNeeded && plasteelFound >= plasteelNeeded)
            return true;

        // 2. Inventory slots (catches things the xform child enumeration might miss)
        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            CheckCandidate(item);

            foreach (var container in _container.GetAllContainers(item))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    CheckCandidate(contained);
                }
            }
        }

        // Early exit if already found enough
        if (steelFound >= steelNeeded && plasteelFound >= plasteelNeeded)
            return true;

        // 3. Entities on the ground near the user (only if still need materials)
        var userCoords = _transform.GetMapCoordinates(user);
        foreach (var nearby in _lookup.GetEntitiesInRange(userCoords, GroundSearchRange))
        {
            CheckCandidate(nearby);
        }

        return steelFound >= steelNeeded && plasteelFound >= plasteelNeeded;
    }

    /// <summary>
    /// Consumes a specified amount from a list of stacks.
    /// </summary>
    private void ConsumeMaterials(List<(EntityUid Uid, StackComponent Stack)> stacks, int amount)
    {
        var remaining = amount;
        foreach (var (uid, stack) in stacks)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(remaining, stack.Count);
            _stack.Use(uid, take, stack);
            remaining -= take;
        }
    }
}
