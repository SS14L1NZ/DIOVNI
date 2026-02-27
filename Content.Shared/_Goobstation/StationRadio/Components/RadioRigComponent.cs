using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RadioRigComponent : Component
{
	[DataField]
	public bool VoiceRelayEnabled = true;

	[DataField]
	public float VoiceRelayRange = 4f;

	[DataField]
	public bool UnobstructedRelayRequired = true;
}
