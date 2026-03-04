using Content.Shared.Armor;
using Content.Shared.Explosion.Components;

namespace Content.Shared.Explosion.EntitySystems;

/// <summary>
/// Lets code in shared trigger explosions and handles explosion resistance examining.
/// All processing is still done clientside.
/// </summary>
public abstract class SharedExplosionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExplosionResistanceComponent, ArmorExamineEvent>(OnArmorExamine);
    }

    private void OnArmorExamine(Entity<ExplosionResistanceComponent> ent, ref ArmorExamineEvent args)
    {
        var value = MathF.Round((1f - ent.Comp.DamageCoefficient) * 100, 1);

        if (value == 0)
            return;

        // DV: Word-based explosion resistance description
        var rating = GetExplosionRating(value);
        var ratingColor = GetExplosionColor(value);

        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString("explosion-resistance-description",
            ("rating", $"[color={ratingColor}]{Loc.GetString(rating)}[/color]")));
    }

    /// <summary>
    /// DV: Returns a localization key for a word-based explosion resistance rating.
    /// </summary>
    private static string GetExplosionRating(float protectionPercent)
    {
        return protectionPercent switch
        {
            >= 50f => "explosion-rating-exceptional",
            >= 35f => "explosion-rating-good",
            >= 20f => "explosion-rating-moderate",
            >= 10f => "explosion-rating-weak",
            _ => "explosion-rating-negligible",
        };
    }

    /// <summary>
    /// DV: Returns a color for the explosion resistance rating.
    /// </summary>
    private static string GetExplosionColor(float protectionPercent)
    {
        return protectionPercent switch
        {
            >= 50f => "#50ff50",
            >= 35f => "#b0ff50",
            >= 20f => "#ffff50",
            >= 10f => "#ff8050",
            _ => "#ff5050",
        };
    }

    /// <summary>
    ///     Given an entity with an explosive component, spawn the appropriate explosion.
    /// </summary>
    /// <remarks>
    ///     Also accepts radius or intensity arguments. This is useful for explosives where the intensity is not
    ///     specified in the yaml / by the component, but determined dynamically (e.g., by the quantity of a
    ///     solution in a reaction).
    /// </remarks>
    public virtual void TriggerExplosive(EntityUid uid, ExplosiveComponent? explosive = null, bool delete = true, float? totalIntensity = null, float? radius = null, EntityUid? user = null)
    {
    }
}
