using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationRadioReceiverComponent : Component
{
    [DataField]
    public uint Frequency = 145;

    /// <summary>
    /// The sound entity being played
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SoundEntity;

    [DataField]
    public string? CurrentMediaPath;

    [DataField]
    public TimeSpan? CurrentMediaStart;

    [DataField]
    public TimeSpan NextResync;

    /// <summary>
    /// Is the radio turned on
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    /// Default audio params for the played audio.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AudioParams DefaultParams = AudioParams.Default.WithVolume(3.5f).WithMaxDistance(8f); // 8 is just the edge of the screen usually
}
