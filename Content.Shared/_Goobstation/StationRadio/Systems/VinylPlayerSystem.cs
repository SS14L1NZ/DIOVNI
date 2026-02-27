using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared.Destructible;
using Content.Shared.DeviceLinking;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Goobstation.StationRadio.Systems;

public sealed class VinylPlayerSystem : EntitySystem
{

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VinylPlayerComponent, EntInsertedIntoContainerMessage>(OnVinylInserted);
        SubscribeLocalEvent<VinylPlayerComponent, EntRemovedFromContainerMessage>(OnVinylRemove);
        SubscribeLocalEvent<VinylPlayerComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<VinylPlayerComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
        if (comp.SoundEntity != null && !args.Powered)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        if (args.Powered)
            return;

        var linkedServers = GetLinkedServers(uid);
        if (linkedServers.Count == 0)
            return;

        StopBroadcastOnServers(linkedServers);
        RefreshAllReceivers();
    }

    private void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
        var linkedServers = GetLinkedServers(uid);
        if (linkedServers.Count == 0)
            return;

        StopBroadcastOnServers(linkedServers);
        RefreshAllReceivers();
    }

    private void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp(args.Entity, out VinylComponent? vinylcomp) || _net.IsClient || vinylcomp.Song == null || !_power.IsPowered(uid))
            return;

        var audio = _audio.PlayPredicted(vinylcomp.Song, uid, uid, AudioParams.Default.WithVolume(3f).WithMaxDistance(4.5f));
        if (audio != null)
            comp.SoundEntity = audio.Value.Entity;

        // Used by VinylSummonRuleSystem
        var ev = new VinylInsertedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        var linkedServers = GetLinkedServers(uid);
        if (linkedServers.Count == 0)
            return;

        StartBroadcastOnServers(linkedServers, vinylcomp.Song);
        RefreshAllReceivers();
    }

    private void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
        if (comp.SoundEntity != null)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        // Used by VinylSummonRuleSystem
        var ev = new VinylRemovedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        var linkedServers = GetLinkedServers(uid);
        if (linkedServers.Count == 0)
            return;

        StopBroadcastOnServers(linkedServers);
        RefreshAllReceivers();
    }

    private HashSet<EntityUid> GetLinkedServers(EntityUid uid)
    {
        var linkedServers = new HashSet<EntityUid>();

        if (TryComp<DeviceLinkSourceComponent>(uid, out var source))
        {
            foreach (var linked in source.LinkedPorts.Keys)
            {
                if (!HasComp<RadioRigComponent>(linked))
                    continue;

                if (!TryComp<DeviceLinkSinkComponent>(linked, out var sink))
                    continue;

                foreach (var server in sink.LinkedSources)
                {
                    if (HasComp<StationRadioServerComponent>(server))
                        linkedServers.Add(server);
                }
            }
        }

        return linkedServers;
    }

    private void StartBroadcastOnServers(HashSet<EntityUid> servers, SoundPathSpecifier song)
    {
        var songPath = song.Path.ToString();

        foreach (var server in servers)
        {
            if (!TryComp<StationRadioServerComponent>(server, out var serverComp))
                continue;

            serverComp.CurrentMediaPath = songPath;
            serverComp.CurrentMediaStart = _timing.CurTime;
            serverComp.Broadcasting = true;
        }
    }

    private void StopBroadcastOnServers(HashSet<EntityUid> servers)
    {
        foreach (var server in servers)
        {
            if (!TryComp<StationRadioServerComponent>(server, out var serverComp))
                continue;

            serverComp.Broadcasting = false;
            serverComp.CurrentMediaPath = null;
            serverComp.CurrentMediaStart = null;
        }
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
