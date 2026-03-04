using Robust.Shared.GameStates;

namespace Content.Shared._DV.ItemQuality;

/// <summary>
/// Marker component for appraiser glasses/equipment.
/// When worn by a player, allows seeing exact numeric values
/// for armor protection and item quality/wear instead of word descriptions.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ItemAppraiserComponent : Component
{
}
