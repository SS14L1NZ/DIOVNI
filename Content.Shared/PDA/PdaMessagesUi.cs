using Robust.Shared.Serialization;

namespace Content.Shared.PDA;

[Serializable, NetSerializable]
public sealed class PdaToggleFlashlightMessage : BoundUserInterfaceMessage
{
    public PdaToggleFlashlightMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaShowRingtoneMessage : BoundUserInterfaceMessage
{
    public PdaShowRingtoneMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaShowUplinkMessage : BoundUserInterfaceMessage
{
    public PdaShowUplinkMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaLockUplinkMessage : BoundUserInterfaceMessage
{
    public PdaLockUplinkMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaShowMusicMessage : BoundUserInterfaceMessage
{
    public PdaShowMusicMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaRequestUpdateInterfaceMessage : BoundUserInterfaceMessage
{
    public PdaRequestUpdateInterfaceMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaToggleStationRadioMessage : BoundUserInterfaceMessage
{
    public PdaToggleStationRadioMessage() { }
}

[Serializable, NetSerializable]
public sealed class PdaSelectStationRadioFrequencyMessage : BoundUserInterfaceMessage
{
    public int Frequency;

    public PdaSelectStationRadioFrequencyMessage(int frequency)
    {
        Frequency = frequency;
    }
}

[Serializable, NetSerializable]
public sealed class PdaScanStationRadioMessage : BoundUserInterfaceMessage
{
    public PdaScanStationRadioMessage() { }
}
