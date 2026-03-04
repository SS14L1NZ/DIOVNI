using Robust.Shared.GameStates;

namespace Content.Shared._DV.ItemQuality;

/// <summary>
/// Added to entities (mobs) that are holding a weapon with ItemQualityComponent.
/// Tracks which held items can passively shield damage.
/// Managed by the server-side ItemQualitySystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponShieldUserComponent : Component
{
}
