using Content.Shared._Goobstation.StationRadio;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._Goobstation.StationRadio.UI;

[UsedImplicitly]
public sealed class StationRadioBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private StationRadioMenu? _menu;

    public StationRadioBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new StationRadioMenu();
        _menu.OnFrequencyChanged += OnFrequencyChanged;
        _menu.OnBroadcastNameChanged += name => SendMessage(new SetStationRadioBroadcastNameMessage(name));
        _menu.OnActiveToggled += active => SendMessage(new ToggleStationRadioReceiverMessage(active));
        _menu.OnClose += Close;
        _menu.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not StationRadioBoundUiState stationState)
            return;

        _menu?.UpdateState(stationState);
    }

    private void OnFrequencyChanged(string frequency)
    {
        if (int.TryParse(frequency.Trim(), out var intFreq) && intFreq > 0)
            SendMessage(new SelectStationRadioFrequencyMessage(intFreq));
        else
            SendMessage(new SelectStationRadioFrequencyMessage(-1));
    }
}
