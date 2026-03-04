using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.ItemQuality;

/// <summary>
/// DoAfter event for weapon repair using a repair kit.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WeaponRepairDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
/// DoAfter event for armor repair using a welder.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ArmorRepairDoAfterEvent : SimpleDoAfterEvent
{
}
