using Content.Shared._DV.Clothing.Events; // DeltaV - Introduce ClothingSlowResistance to Species
using Content.Shared.Administration.Managers;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Verbs;
using Content.Shared._DV.ItemQuality;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Clothing;

public sealed class ClothingSpeedModifierSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingSpeedModifierComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<ClothingSpeedModifierComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<ClothingSpeedModifierComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<ClothingSpeedModifierComponent, GetVerbsEvent<ExamineVerb>>(OnClothingVerbExamine);
        SubscribeLocalEvent<ClothingSpeedModifierComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnGetState(EntityUid uid, ClothingSpeedModifierComponent component, ref ComponentGetState args)
    {
        args.State = new ClothingSpeedModifierComponentState(component.WalkModifier, component.SprintModifier);
    }

    private void OnHandleState(EntityUid uid, ClothingSpeedModifierComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not ClothingSpeedModifierComponentState state)
            return;

        var diff = !MathHelper.CloseTo(component.SprintModifier, state.SprintModifier) ||
                   !MathHelper.CloseTo(component.WalkModifier, state.WalkModifier);

        component.WalkModifier = state.WalkModifier;
        component.SprintModifier = state.SprintModifier;

        // Avoid raising the event for the container if nothing changed.
        // We'll still set the values in case they're slightly different but within tolerance.
        if (diff && _container.TryGetContainingContainer((uid, null, null), out var container))
        {
            _movementSpeed.RefreshMovementSpeedModifiers(container.Owner);
        }
    }

    private void OnRefreshMoveSpeed(EntityUid uid, ClothingSpeedModifierComponent component, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        // DeltaV Start - Introduce ClothingSlowResistance to Species
        if (!_toggle.IsActivated(uid))
            return;

        if (_container.TryGetContainingContainer((uid, null), out var container))
        {
            var ev = new ModifyClothingSlowdownEvent(component.WalkModifier, component.SprintModifier);
            RaiseLocalEvent(container.Owner, ref ev);

            args.Args.ModifySpeed(ev.WalkModifier, ev.RunModifier);
        }
        else
        {
            args.Args.ModifySpeed(component.WalkModifier, component.SprintModifier);
        }
        // DeltaV End - Introduce ClothingSlowResistance to Species
    }

    private void OnClothingVerbExamine(EntityUid uid, ClothingSpeedModifierComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // DV: Speed modifier stats require appraiser glasses (admin ghosts exempt)
        var isAdminGhost = HasComp<GhostComponent>(args.User) && _admin.IsAdmin(args.User);
        if (!isAdminGhost)
        {
            if (!_inventory.TryGetSlotEntity(args.User, "eyes", out var eyes) ||
                !HasComp<ItemAppraiserComponent>(eyes))
                return;
        }

        // DeltaV Start - Introduce ClothingSlowResistance to Species
        var ev = new ModifyClothingSlowdownEvent(component.WalkModifier, component.SprintModifier);
        RaiseLocalEvent(args.User, ref ev);

        var walkModifierPercentage = MathF.Round((1.0f - ev.WalkModifier) * 100f, 1);
        var sprintModifierPercentage = MathF.Round((1.0f - ev.RunModifier) * 100f, 1);
        // DeltaV End - Introduce ClothingSlowResistance to Species

        if (walkModifierPercentage == 0.0f && sprintModifierPercentage == 0.0f)
            return;

        // DV: Word-based speed descriptions instead of exact percentages
        var msg = new FormattedMessage();
        var avgEffect = Math.Max(MathF.Abs(walkModifierPercentage), MathF.Abs(sprintModifierPercentage));
        var isSlowdown = walkModifierPercentage > 0 || sprintModifierPercentage > 0;

        var rating = GetSpeedRating(avgEffect, isSlowdown);
        var ratingColor = GetSpeedColor(avgEffect, isSlowdown);
        msg.AddMarkupOrThrow($"[color={ratingColor}]{Loc.GetString(rating)}[/color]");

        _examine.AddDetailedExamineVerb(args, component, msg, Loc.GetString("clothing-speed-examinable-verb-text"), "/Textures/Interface/VerbIcons/outfit.svg.192dpi.png", Loc.GetString("clothing-speed-examinable-verb-message"));
    }

    /// <summary>
    /// DV: Returns a localization key for a word-based speed rating.
    /// </summary>
    private static string GetSpeedRating(float percentEffect, bool isSlowdown)
    {
        if (isSlowdown)
        {
            return percentEffect switch
            {
                >= 25f => "clothing-speed-rating-severe-slowdown",
                >= 15f => "clothing-speed-rating-significant-slowdown",
                >= 8f => "clothing-speed-rating-moderate-slowdown",
                >= 3f => "clothing-speed-rating-light-slowdown",
                _ => "clothing-speed-rating-negligible-slowdown",
            };
        }

        return percentEffect switch
        {
            >= 25f => "clothing-speed-rating-major-boost",
            >= 15f => "clothing-speed-rating-significant-boost",
            >= 8f => "clothing-speed-rating-moderate-boost",
            >= 3f => "clothing-speed-rating-light-boost",
            _ => "clothing-speed-rating-negligible-boost",
        };
    }

    /// <summary>
    /// DV: Returns a color for the speed rating display.
    /// </summary>
    private static string GetSpeedColor(float percentEffect, bool isSlowdown)
    {
        if (isSlowdown)
        {
            return percentEffect switch
            {
                >= 25f => "#ff3030",   // bright red
                >= 15f => "#ff6050",   // red-orange
                >= 8f => "#ff8050",    // orange
                >= 3f => "#ffaa50",    // yellow-orange
                _ => "#ffcc50",        // yellow
            };
        }

        return percentEffect switch
        {
            >= 25f => "#50ff50",   // bright green
            >= 15f => "#80ff50",   // light green
            >= 8f => "#b0ff50",    // yellow-green
            >= 3f => "#d0ff50",    // lime
            _ => "#e0ff80",        // pale lime
        };
    }

    private void OnToggled(Entity<ClothingSpeedModifierComponent> ent, ref ItemToggledEvent args)
    {
        // make sentient boots slow or fast too
        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
        {
            // inventory system will automatically hook into the event raised by this and update accordingly
            _movementSpeed.RefreshMovementSpeedModifiers(container.Owner);
        }
    }
}
