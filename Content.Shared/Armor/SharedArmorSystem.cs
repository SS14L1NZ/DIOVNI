using Content.Shared.Administration.Managers;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Content.Shared._DV.ItemQuality;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

/// <summary>
///     This handles logic relating to <see cref="ArmorComponent" />
/// </summary>
public abstract class SharedArmorSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
        SubscribeLocalEvent<ArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
    }

    /// <summary>
    /// Get the total Damage reduction value of all equipment caught by the relay.
    /// </summary>
    /// <param name="ent">The item that's being relayed to</param>
    /// <param name="args">The event, contains the running count of armor percentage as a coefficient</param>
    private void OnCoefficientQuery(Entity<ArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        foreach (var armorCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
        }
    }

    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration)); // Goob edit
    }

    private void OnBorgDamageModify(EntityUid uid, ArmorComponent component,
        ref BorgModuleRelayedEvent<DamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration)); // Goob edit
    }

    private void OnArmorVerbExamine(EntityUid uid, ArmorComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // DV: Detailed armor stats require appraiser glasses (admin ghosts exempt)
        var isAdminGhost = HasComp<GhostComponent>(args.User) && _admin.IsAdmin(args.User);
        if (!isAdminGhost)
        {
            if (!_inventory.TryGetSlotEntity(args.User, "eyes", out var eyes) ||
                !HasComp<ItemAppraiserComponent>(eyes))
                return;
        }

        // DV: always show word-based descriptions; exact numbers only via appraiser lens UI
        var examineMarkup = GetArmorExamine(component.Modifiers);

        var ev = new ArmorExamineEvent(examineMarkup);
        RaiseLocalEvent(uid, ref ev);

        _examine.AddDetailedExamineVerb(args, component, examineMarkup,
            Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    private FormattedMessage GetArmorExamine(DamageModifierSet armorModifiers)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        foreach (var coefficientArmor in armorModifiers.Coefficients)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            var protectionPercent = MathF.Round((1f - coefficientArmor.Value) * 100, 1);

            // DV: word-based description always
            var rating = GetProtectionRating(protectionPercent);
            var ratingColor = GetProtectionColor(protectionPercent);
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-description",
                ("type", armorType),
                ("rating", $"[color={ratingColor}]{Loc.GetString(rating)}[/color]")
            ));
        }

        foreach (var flatArmor in armorModifiers.FlatReduction)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());
            var flatPercent = MathF.Min(flatArmor.Value * 5f, 100f); // rough scale for flat to rating
            var rating = GetProtectionRating(flatPercent);
            var ratingColor = GetProtectionColor(flatPercent);
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-description",
                ("type", armorType),
                ("rating", $"[color={ratingColor}]{Loc.GetString(rating)}[/color]")
            ));
        }

        return msg;
    }

    /// <summary>
    /// DV: Returns a localization key for a word-based protection rating.
    /// </summary>
    private static string GetProtectionRating(float protectionPercent)
    {
        return protectionPercent switch
        {
            >= 50f => "armor-rating-exceptional",
            >= 35f => "armor-rating-good",
            >= 20f => "armor-rating-moderate",
            >= 10f => "armor-rating-weak",
            >= 1f => "armor-rating-negligible",
            _ => "armor-rating-none",
        };
    }

    /// <summary>
    /// DV: Returns a color for the protection rating display.
    /// </summary>
    private static string GetProtectionColor(float protectionPercent)
    {
        return protectionPercent switch
        {
            >= 50f => "#50ff50",   // bright green
            >= 35f => "#b0ff50",   // yellow-green
            >= 20f => "#ffff50",   // yellow
            >= 10f => "#ff8050",   // orange
            >= 1f => "#ff5050",    // red
            _ => "#808080",        // gray
        };
    }
}
