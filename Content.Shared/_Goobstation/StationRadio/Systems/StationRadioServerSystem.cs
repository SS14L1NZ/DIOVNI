using Content.Shared._Goobstation.StationRadio;
using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared.UserInterface;

namespace Content.Shared._Goobstation.StationRadio.Systems;

public sealed class StationRadioServerSystem : EntitySystem
{
    private const int MinFrequency = 1;
    private const int MaxFrequency = 9999;

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioServerComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<StationRadioServerComponent, SelectStationRadioFrequencyMessage>(OnSetFrequency);
        SubscribeLocalEvent<StationRadioServerComponent, SetStationRadioBroadcastNameMessage>(OnSetName);
        SubscribeLocalEvent<StationRadioServerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, StationRadioServerComponent comp, MapInitEvent args)
    {
        UpdateUi((uid, comp));
    }

    private void OnBeforeUiOpen(Entity<StationRadioServerComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSetFrequency(Entity<StationRadioServerComponent> ent, ref SelectStationRadioFrequencyMessage args)
    {
        if (!args.Actor.Valid)
            return;

        if (args.Frequency < MinFrequency || args.Frequency > MaxFrequency)
        {
            UpdateUi(ent);
            return;
        }

        var oldFrequency = ent.Comp.Frequency;
        var newFrequency = (uint) args.Frequency;

        if (oldFrequency == newFrequency)
        {
            UpdateUi(ent);
            return;
        }

        ent.Comp.Frequency = newFrequency;

        UpdateUi(ent);
        RefreshReceiversForFrequencies(oldFrequency, newFrequency);
    }

    private void OnSetName(Entity<StationRadioServerComponent> ent, ref SetStationRadioBroadcastNameMessage args)
    {
        if (!args.Actor.Valid)
            return;

        ent.Comp.BroadcastName = args.Name.Trim();
        if (string.IsNullOrWhiteSpace(ent.Comp.BroadcastName))
            ent.Comp.BroadcastName = "Station Radio";

        UpdateUi(ent);
    }

    private void UpdateUi(Entity<StationRadioServerComponent> ent)
    {
        _ui.SetUiState(ent.Owner,
            StationRadioUiKey.Key,
            new StationRadioBoundUiState(ent.Comp.Frequency, null, ent.Comp.BroadcastName, true));
    }

    private void RefreshReceiversForFrequencies(uint frequencyA, uint frequencyB)
    {
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var comp))
        {
            if (comp.Frequency != frequencyA && comp.Frequency != frequencyB)
                continue;

            RaiseLocalEvent(receiver, new StationRadioRefreshEvent());
        }
    }
}
