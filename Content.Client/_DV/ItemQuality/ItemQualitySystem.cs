using Content.Client._DV.ItemQuality.UI;
using Content.Client.Administration.Managers;
using Content.Client.Examine;
using Content.Shared._DV.ItemQuality;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Client.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameStates;

namespace Content.Client._DV.ItemQuality;

/// <summary>
/// Client-side system for item quality.
/// Applies a wear-based color tint to both the item sprite and equipped clothing layers.
/// Weapons get a rust-colored tint, clothing/armor gets a dirt tint.
/// The tint intensifies as wear decreases.
/// Also handles toggleable clothing (hardsuit/modsuit helmets) by propagating wear from the parent.
/// </summary>
public sealed class ItemQualitySystem : SharedItemQualitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IClientAdminManager _adminManager = default!;

    /// <summary>
    /// The currently open appraiser lens window, if any.
    /// </summary>
    private AppraiserLensWindow? _lensWindow;

    /// <summary>
    /// Worn weapon tint — dark reddish-brown (rust).
    /// </summary>
    private static readonly Color WeaponWornTint = new(0.55f, 0.40f, 0.30f);

    /// <summary>
    /// Worn clothing/armor tint — yellowish-brown (dirt/grime).
    /// </summary>
    private static readonly Color ClothingWornTint = new(0.70f, 0.63f, 0.48f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemQualityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ItemQualityComponent, AfterAutoHandleStateEvent>(OnStateChanged);

        // Tint equipped clothing layers on the character
        SubscribeLocalEvent<ItemQualityComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
        SubscribeLocalEvent<ItemQualityComponent, HeldVisualsUpdatedEvent>(OnHeldVisualsUpdated);

        // Tint toggleable clothing (hardsuit/modsuit helmets) using the parent's quality
        SubscribeLocalEvent<AttachedClothingComponent, EquipmentVisualsUpdatedEvent>(OnAttachedEquipmentVisualsUpdated);

        // Open appraiser lens UI when examining quality items while wearing appraiser glasses
        SubscribeLocalEvent<ItemQualityComponent, ClientExaminedEvent>(OnClientExamined);
    }

    private void OnStartup(EntityUid uid, ItemQualityComponent component, ComponentStartup args)
    {
        UpdateItemSprite(uid, component);
    }

    private void OnStateChanged(EntityUid uid, ItemQualityComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateItemSprite(uid, component);

        // Update equipped layers if the item is currently worn
        UpdateEquippedLayers(uid, component);

        // Also update any attached clothing (helmet layers) that inherits wear tint
        UpdateAttachedClothingLayers(uid, component);
    }

    /// <summary>
    /// When clothing is rendered on a character, tint the clothing layers based on wear.
    /// </summary>
    private void OnEquipmentVisualsUpdated(EntityUid uid, ItemQualityComponent component, EquipmentVisualsUpdatedEvent args)
    {
        if (args.RevealedLayers.Count == 0)
            return;

        if (!TryComp(args.Equipee, out SpriteComponent? sprite))
            return;

        var tint = GetWearTint(uid, component);
        ApplyTintToLayers(args.Equipee, sprite, args.RevealedLayers, tint);
    }

    /// <summary>
    /// When item is held in hand, tint the hand layers based on wear.
    /// </summary>
    private void OnHeldVisualsUpdated(EntityUid uid, ItemQualityComponent component, HeldVisualsUpdatedEvent args)
    {
        if (args.RevealedLayers.Count == 0)
            return;

        if (!TryComp(args.User, out SpriteComponent? sprite))
            return;

        var tint = GetWearTint(uid, component);
        ApplyTintToLayers(args.User, sprite, args.RevealedLayers, tint);
    }

    /// <summary>
    /// When a toggleable attached clothing (e.g. hardsuit/modsuit helmet) is equipped on a character,
    /// propagate the wear tint from the parent entity (the suit itself).
    /// </summary>
    private void OnAttachedEquipmentVisualsUpdated(EntityUid uid, AttachedClothingComponent attached, EquipmentVisualsUpdatedEvent args)
    {
        if (args.RevealedLayers.Count == 0)
            return;

        if (!TryComp<ItemQualityComponent>(attached.AttachedUid, out var quality))
            return;

        if (!TryComp(args.Equipee, out SpriteComponent? sprite))
            return;

        var tint = GetWearTint(attached.AttachedUid, quality);
        ApplyTintToLayers(args.Equipee, sprite, args.RevealedLayers, tint);
    }

    /// <summary>
    /// When a player wearing appraiser glasses (or admin ghost) examines an entity with quality data,
    /// open the lens focusing window to reveal exact stats.
    /// Admin ghosts bypass the glasses check and QTE — data is sent directly.
    /// </summary>
    private void OnClientExamined(EntityUid uid, ItemQualityComponent component, ClientExaminedEvent args)
    {
        if (args.Examiner != _player.LocalEntity)
            return;

        var isAdminGhost = HasComp<GhostComponent>(args.Examiner) && _adminManager.IsActive();

        if (!isAdminGhost)
        {
            if (!_inventorySystem.TryGetSlotEntity(args.Examiner, "eyes", out var eyes) ||
                !HasComp<ItemAppraiserComponent>(eyes))
                return;
        }

        // Admin ghosts skip the QTE — send scan immediately
        if (isAdminGhost)
        {
            RaiseNetworkEvent(new AppraiserScanRequestEvent(GetNetEntity(uid)));
            return;
        }

        OpenLensWindow(uid);
    }

    /// <summary>
    /// Opens the appraiser lens focusing window for the target entity.
    /// </summary>
    private void OpenLensWindow(EntityUid target)
    {
        var name = Name(target);

        if (_lensWindow == null)
        {
            _lensWindow = new AppraiserLensWindow();
            _lensWindow.OnClose += () => _lensWindow = null;
            _lensWindow.OnAppraisalComplete += OnAppraisalCompleted;
        }

        _lensWindow.SetTarget(target, name);
        _lensWindow.OpenRemembered();
    }

    /// <summary>
    /// When the QTE minigame is completed, send a scan request to the server
    /// so the data is recorded in the PDA archive cartridge.
    /// </summary>
    private void OnAppraisalCompleted(EntityUid target)
    {
        if (!target.IsValid())
            return;

        RaiseNetworkEvent(new AppraiserScanRequestEvent(GetNetEntity(target)));
    }

    /// <summary>
    /// Re-applies tint to equipped clothing layers when wear changes via network state.
    /// </summary>
    private void UpdateEquippedLayers(EntityUid uid, ItemQualityComponent component)
    {
        if (!component.IsWorn)
            return;

        if (!TryComp<ClothingComponent>(uid, out var clothing) || clothing.InSlot == null)
            return;

        var xform = Transform(uid);
        if (xform.ParentUid is not { Valid: true } parentUid)
            return;

        if (!TryComp(parentUid, out SpriteComponent? parentSprite))
            return;

        if (!TryComp(parentUid, out InventorySlotsComponent? inventorySlots))
            return;

        if (!inventorySlots.VisualLayerKeys.TryGetValue(clothing.InSlot, out var layerKeys))
            return;

        var tint = GetWearTint(uid, component);

        foreach (var key in layerKeys)
        {
            if (!_sprite.LayerMapTryGet((parentUid, parentSprite), key, out var index, false))
                continue;

            _sprite.LayerSetColor((parentUid, parentSprite), index, tint);
        }
    }

    /// <summary>
    /// When the parent suit's wear state changes, also update any attached clothing (helmets)
    /// so their layers on the wearer also reflect the wear tint.
    /// </summary>
    private void UpdateAttachedClothingLayers(EntityUid uid, ItemQualityComponent component)
    {
        if (!TryComp<ToggleableClothingComponent>(uid, out var toggleable))
            return;

        var tint = GetWearTint(uid, component);

        foreach (var (attachedUid, _) in toggleable.ClothingUids)
        {
            if (!TryComp<ClothingComponent>(attachedUid, out var attachedClothing))
                continue;

            if (attachedClothing.InSlot == null)
                continue;

            var attachedXform = Transform(attachedUid);
            if (attachedXform.ParentUid is not { Valid: true } wearerUid)
                continue;

            if (!TryComp(wearerUid, out SpriteComponent? wearerSprite))
                continue;

            if (!TryComp(wearerUid, out InventorySlotsComponent? wearerSlots))
                continue;

            if (!wearerSlots.VisualLayerKeys.TryGetValue(attachedClothing.InSlot, out var layerKeys))
                continue;

            foreach (var key in layerKeys)
            {
                if (!_sprite.LayerMapTryGet((wearerUid, wearerSprite), key, out var index, false))
                    continue;

                _sprite.LayerSetColor((wearerUid, wearerSprite), index, tint);
            }
        }
    }

    /// <summary>
    /// Tints the item's own SpriteComponent (visible on ground, in inventory, etc.)
    /// </summary>
    private void UpdateItemSprite(EntityUid uid, ItemQualityComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.SetColor((uid, sprite), GetWearTint(uid, component));
    }

    /// <summary>
    /// Shared helper: apply a tint color to a set of revealed layers on a sprite.
    /// </summary>
    private void ApplyTintToLayers(EntityUid spriteOwner, SpriteComponent sprite, HashSet<string> layers, Color tint)
    {
        foreach (var key in layers)
        {
            if (!_sprite.LayerMapTryGet((spriteOwner, sprite), key, out var index, false))
                continue;

            _sprite.LayerSetColor((spriteOwner, sprite), index, tint);
        }
    }

    /// <summary>
    /// Calculates the wear tint color. Pristine = white. Worn = yellowish-brown or rust.
    /// Tinting starts when wear drops below 80%.
    /// </summary>
    private Color GetWearTint(EntityUid uid, ItemQualityComponent component)
    {
        var wearFactor = 1f - component.Wear;

        if (wearFactor < 0.2f)
            return Color.White;

        var tintStrength = (wearFactor - 0.2f) / 0.8f;

        var isWeapon = HasComp<GunComponent>(uid) || HasComp<MeleeWeaponComponent>(uid);
        var targetTint = isWeapon ? WeaponWornTint : ClothingWornTint;

        return new Color(
            1f - (1f - targetTint.R) * tintStrength,
            1f - (1f - targetTint.G) * tintStrength,
            1f - (1f - targetTint.B) * tintStrength);
    }
}
