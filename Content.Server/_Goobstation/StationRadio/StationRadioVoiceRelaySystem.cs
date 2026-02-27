using Content.Server.Chat.Systems;
using Content.Server.Interaction;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared.DeviceLinking;

namespace Content.Server._Goobstation.StationRadio;

public sealed class StationRadioVoiceRelaySystem : EntitySystem
{
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly RadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
    }

    private void OnEntitySpoke(EntitySpokeEvent args)
    {
        if (args.Channel != null)
            return;

        if (!TryGetNearestRelayRig(args.Source, out var rigUid, out var rigComp))
            return;

        var frequencies = new HashSet<uint>();
        foreach (var server in GetLinkedServers(rigUid))
        {
            if (!TryComp<StationRadioServerComponent>(server, out var serverComp))
                continue;

            if (!rigComp.VoiceRelayEnabled)
                continue;

            if (!frequencies.Add(serverComp.Frequency))
                continue;

            _radio.SendRadioMessage(args.Source,
                args.Message,
                "RadioShow",
                server,
                frequency: (int) serverComp.Frequency,
                language: args.Language);
        }
    }

    private bool TryGetNearestRelayRig(EntityUid speaker, out EntityUid rigUid, out RadioRigComponent rigComp)
    {
        rigUid = EntityUid.Invalid;
        rigComp = default!;

        var sourceCoords = Transform(speaker).Coordinates;
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<RadioRigComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.VoiceRelayEnabled)
                continue;

            if (!HasAnyLinkedServer(uid))
                continue;

            var rigCoords = Transform(uid).Coordinates;
            if (!sourceCoords.TryDistance(EntityManager, rigCoords, out var distance))
                continue;

            if (distance > comp.VoiceRelayRange)
                continue;

            if (comp.UnobstructedRelayRequired && !_interaction.InRangeUnobstructed(speaker, uid, comp.VoiceRelayRange))
                continue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            rigUid = uid;
            rigComp = comp;
        }

        return rigUid != EntityUid.Invalid;
    }

    private bool HasAnyLinkedServer(EntityUid rigUid)
    {
        if (!TryComp<DeviceLinkSinkComponent>(rigUid, out var sink))
            return false;

        foreach (var source in sink.LinkedSources)
        {
            if (HasComp<StationRadioServerComponent>(source))
                return true;
        }

        return false;
    }

    private IEnumerable<EntityUid> GetLinkedServers(EntityUid rigUid)
    {
        if (!TryComp<DeviceLinkSinkComponent>(rigUid, out var sink))
            yield break;

        foreach (var source in sink.LinkedSources)
        {
            if (HasComp<StationRadioServerComponent>(source))
                yield return source;
        }
    }
}
