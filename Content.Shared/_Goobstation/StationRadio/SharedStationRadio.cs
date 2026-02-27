using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.StationRadio;

[Serializable, NetSerializable]
public enum StationRadioUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StationRadioBoundUiState : BoundUserInterfaceState
{
    public uint Frequency;
    public bool? Active;
    public string? BroadcastName;
    public bool CanEditBroadcastName;

    public StationRadioBoundUiState(uint frequency, bool? active, string? broadcastName = null, bool canEditBroadcastName = false)
    {
        Frequency = frequency;
        Active = active;
        BroadcastName = broadcastName;
        CanEditBroadcastName = canEditBroadcastName;
    }
}

[Serializable, NetSerializable]
public sealed class SelectStationRadioFrequencyMessage : BoundUserInterfaceMessage
{
    public int Frequency;

    public SelectStationRadioFrequencyMessage(int frequency)
    {
        Frequency = frequency;
    }
}

[Serializable, NetSerializable]
public sealed class ToggleStationRadioReceiverMessage : BoundUserInterfaceMessage
{
    public bool Active;

    public ToggleStationRadioReceiverMessage(bool active)
    {
        Active = active;
    }
}

[Serializable, NetSerializable]
public sealed class SetStationRadioBroadcastNameMessage : BoundUserInterfaceMessage
{
    public string Name;

    public SetStationRadioBroadcastNameMessage(string name)
    {
        Name = name;
    }
}
