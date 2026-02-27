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

        if (ent.Comp.VoiceRelayEnabled == args.Active)
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

        var changedFrequencies = new HashSet<uint>();
        var requestedFrequency = (uint) args.Frequency;
        var linkedServers = GetLinkedServers(ent.Owner);
        foreach (var server in linkedServers)
        {
            if (!TryComp<StationRadioServerComponent>(server, out var serverComp))
                continue;

            if (serverComp.Frequency == requestedFrequency)
                continue;

            changedFrequencies.Add(serverComp.Frequency);
            changedFrequencies.Add(requestedFrequency);
            serverComp.Frequency = requestedFrequency;
        }

        if (changedFrequencies.Count > 0)
            RefreshReceiversForFrequencies(changedFrequencies);

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

    private void RefreshReceiversForFrequencies(HashSet<uint> frequencies)
    {
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var comp))
        {
            if (!frequencies.Contains(comp.Frequency))
                continue;

            RaiseLocalEvent(receiver, new StationRadioRefreshEvent());
        }
    }
}
