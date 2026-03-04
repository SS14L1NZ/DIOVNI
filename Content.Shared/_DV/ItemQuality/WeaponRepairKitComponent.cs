using Robust.Shared.GameStates;

namespace Content.Shared._DV.ItemQuality;

/// <summary>
/// A repair kit item that can be used to restore weapon condition.
/// When used on a weapon with ItemQualityComponent, initiates a repair doAfter
/// that consumes materials from the user's inventory plus the kit itself.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponRepairKitComponent : Component
{
    /// <summary>
    /// How much wear to restore (0.25 = 25%).
    /// </summary>
    [DataField]
    public float RepairAmount = 0.25f;

    /// <summary>
    /// Required steel sheets in player's inventory.
    /// </summary>
    [DataField]
    public int RequiredSteel = 10;

    /// <summary>
    /// Required plasteel sheets in player's inventory.
    /// </summary>
    [DataField]
    public int RequiredPlasteel = 5;

    /// <summary>
    /// DoAfter delay in seconds.
    /// </summary>
    [DataField]
    public float RepairDelay = 5f;
}
