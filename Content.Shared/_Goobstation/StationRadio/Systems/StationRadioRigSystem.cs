using Content.Shared._Goobstation.StationRadio;
using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared.DeviceLinking;
using Content.Shared.UserInterface;

namespace Content.Shared._Goobstation.StationRadio.Systems;

public sealed class StationRadioRigSystem : EntitySystem
{
    private const int MinFrequency = 1;
    private const int MaxFrequency = 9999;

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioRigComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<RadioRigComponent, ToggleStationRadioReceiverMessage>(OnToggleVoiceRelay);
        SubscribeLocalEvent<RadioRigComponent, SelectStationRadioFrequencyMessage>(OnSetFrequency);
    }

    private void OnBeforeUiOpen(Entity<RadioRigComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnToggleVoiceRelay(Entity<RadioRigComponent> ent, ref ToggleStationRadioReceiverMessage args)
    {
        if (!args.Actor.Valid)
            return;

        ent.Comp.VoiceRelayEnabled = args.Active;
        UpdateUi(ent);
    }

    private void OnSetFrequency(Entity<RadioRigComponent> ent, ref SelectStationRadioFrequencyMessage args)
    {
        if (!args.Actor.Valid)
            return;

        if (args.Frequency < MinFrequency || args.Frequency > MaxFrequency)
        {
            UpdateUi(ent);
            return;
        }

        var linkedServers = GetLinkedServers(ent.Owner);
        foreach (var server in linkedServers)
        {
            if (TryComp<StationRadioServerComponent>(server, out var serverComp))
                serverComp.Frequency = (uint) args.Frequency;
        }

        RefreshAllReceivers();
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<RadioRigComponent> ent)
    {
        uint frequency = 145;
        var linkedServers = GetLinkedServers(ent.Owner);

        foreach (var server in linkedServers)
        {
            if (!TryComp<StationRadioServerComponent>(server, out var serverComp))
                continue;

            frequency = serverComp.Frequency;
            break;
        }

        _ui.SetUiState(ent.Owner, StationRadioUiKey.Key, new StationRadioBoundUiState(frequency, ent.Comp.VoiceRelayEnabled, null, false));
    }

    private HashSet<EntityUid> GetLinkedServers(EntityUid uid)
    {
        var linkedServers = new HashSet<EntityUid>();

        if (!TryComp<DeviceLinkSinkComponent>(uid, out var sink))
            return linkedServers;

        foreach (var sourceUid in sink.LinkedSources)
        {
            if (HasComp<StationRadioServerComponent>(sourceUid))
                linkedServers.Add(sourceUid);
        }

        return linkedServers;
    }

    private void RefreshAllReceivers()
    {
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
        {
            RaiseLocalEvent(receiver, new StationRadioRefreshEvent());
        }
    }
}
