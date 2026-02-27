using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StationRadioServerComponent : Component
{
	[DataField]
	public uint Frequency = 145;

	[DataField]
	public string BroadcastName = "Station Radio";

	[DataField]
	public string? CurrentMediaPath;

	[DataField]
	public TimeSpan? CurrentMediaStart;

	[DataField]
	public bool Broadcasting;
}
