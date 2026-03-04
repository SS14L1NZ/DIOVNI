using Content.Shared.Armor;
using Content.Shared.Damage.Events;
using Content.Shared.Inventory;

namespace Content.Shared.Stunnable;

public sealed partial class StaminaDamageResistanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaminaDamageResistanceComponent, InventoryRelayedEvent<TakeStaminaDamageEvent>>(OnStaminaMeleeHit);
        SubscribeLocalEvent<StaminaDamageResistanceComponent, ArmorExamineEvent>(OnExamine);
    }

    private void OnStaminaMeleeHit(Entity<StaminaDamageResistanceComponent> ent, ref InventoryRelayedEvent<TakeStaminaDamageEvent> args)
    {
        args.Args.Multiplier *= ent.Comp.Coefficient;
    }
    private void OnExamine(Entity<StaminaDamageResistanceComponent> ent, ref ArmorExamineEvent args)
    {
        // Mono - fix floating point error stuff guh
        var value = MathF.Round((1f - ent.Comp.Coefficient) * 100, 1);

        if (value == 0)
            return;

        // DV: Word-based stamina resistance description
        var rating = GetStaminaRating(value);
        var ratingColor = GetStaminaColor(value);

        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString("armor-stamina-description",
            ("rating", $"[color={ratingColor}]{Loc.GetString(rating)}[/color]")));
    }

    /// <summary>
    /// DV: Returns a localization key for a word-based stamina resistance rating.
    /// </summary>
    private static string GetStaminaRating(float protectionPercent)
    {
        return protectionPercent switch
        {
            >= 50f => "stamina-rating-exceptional",
            >= 35f => "stamina-rating-good",
            >= 20f => "stamina-rating-moderate",
            >= 10f => "stamina-rating-weak",
            _ => "stamina-rating-negligible",
        };
    }

    /// <summary>
    /// DV: Returns a color for the stamina resistance rating.
    /// </summary>
    private static string GetStaminaColor(float protectionPercent)
    {
        return protectionPercent switch
        {
            >= 50f => "#50ffff",
            >= 35f => "#80ffcc",
            >= 20f => "#aaffaa",
            >= 10f => "#ccff80",
            _ => "#ffff80",
        };
    }
}
