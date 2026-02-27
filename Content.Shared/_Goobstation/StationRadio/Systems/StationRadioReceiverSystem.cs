using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Content.Shared._Goobstation.StationRadio;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Goobstation.StationRadio.Systems;

public sealed class StationRadioReceiverSystem : EntitySystem
{
    private const int MinFrequency = 1;
    private const int MaxFrequency = 9999;
    private static readonly TimeSpan ResyncInterval = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan BroadcastCacheInterval = TimeSpan.FromMilliseconds(250);

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<uint, (string Path, TimeSpan StartedAt)> _broadcastCache = new();
    private TimeSpan _nextBroadcastCacheRefresh;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<StationRadioReceiverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<StationRadioReceiverComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<StationRadioReceiverComponent, SelectStationRadioFrequencyMessage>(OnSetFrequency);
        SubscribeLocalEvent<StationRadioReceiverComponent, ToggleStationRadioReceiverMessage>(OnToggleReceiver);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        RefreshBroadcastCache(false);

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SoundEntity == null || comp.CurrentMediaStart == null)
                continue;

            if (comp.NextResync > _timing.CurTime)
                continue;

            if (!TryGetBroadcast(comp.Frequency, out var path, out var startedAt))
            {
                StopReceiverSound(comp);
                continue;
            }

            if (comp.CurrentMediaPath != path || comp.CurrentMediaStart != startedAt)
            {
                RefreshReceiver(uid, comp);
                continue;
            }

            if (!_power.IsPowered(uid) || !comp.Active)
            {
                comp.NextResync = _timing.CurTime + ResyncInterval;
                continue;
            }

            var elapsed = Math.Max(0f, (float) (_timing.CurTime - startedAt).TotalSeconds);
            _audio.SetPlaybackPosition(comp.SoundEntity, elapsed);
            comp.NextResync = _timing.CurTime + ResyncInterval;
        }
    }

    private void OnMapInit(EntityUid uid, StationRadioReceiverComponent comp, MapInitEvent args)
    {
        RefreshBroadcastCache(true);
        RefreshReceiver(uid, comp);
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        RefreshReceiver(uid, comp);
    }

    private void OnRefresh(EntityUid uid, StationRadioReceiverComponent comp, ref StationRadioRefreshEvent args)
    {
        RefreshBroadcastCache(true);
        RefreshReceiver(uid, comp);
    }

    private void OnBeforeUiOpen(Entity<StationRadioReceiverComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSetFrequency(Entity<StationRadioReceiverComponent> ent, ref SelectStationRadioFrequencyMessage args)
    {
        if (!args.Actor.Valid)
            return;

        if (args.Frequency < MinFrequency || args.Frequency > MaxFrequency)
        {
            UpdateUi(ent);
            return;
        }

        if (ent.Comp.Frequency == (uint) args.Frequency)
        {
            UpdateUi(ent);
            return;
        }

        ent.Comp.Frequency = (uint) args.Frequency;

        RefreshBroadcastCache(true);
        UpdateUi(ent);
        RefreshReceiver(ent.Owner, ent.Comp);
    }

    private void OnToggleReceiver(Entity<StationRadioReceiverComponent> ent, ref ToggleStationRadioReceiverMessage args)
    {
        if (!args.Actor.Valid)
            return;

        if (ent.Comp.Active == args.Active)
            return;

        ent.Comp.Active = args.Active;
        UpdateUi(ent);
        RefreshReceiver(ent.Owner, ent.Comp);
    }

    private void UpdateUi(Entity<StationRadioReceiverComponent> ent)
    {
        _ui.SetUiState(ent.Owner, StationRadioUiKey.Key, new StationRadioBoundUiState(ent.Comp.Frequency, ent.Comp.Active, null, false));
    }

    private void RefreshReceiver(EntityUid uid, StationRadioReceiverComponent comp)
    {
        if (!TryGetBroadcast(comp.Frequency, out var path, out var startedAt))
        {
            StopReceiverSound(comp);
            return;
        }

        var needsNewSound = comp.SoundEntity == null
                            || !Exists(comp.SoundEntity.Value)
                            || comp.CurrentMediaPath != path
                            || comp.CurrentMediaStart != startedAt;

        if (needsNewSound)
        {
            StopReceiverSound(comp);

            var specifier = new SoundPathSpecifier(path);
            var audio = _audio.PlayPvs(specifier, uid, comp.DefaultParams);
            if (audio == null)
                return;

            comp.SoundEntity = audio.Value.Entity;
            comp.CurrentMediaPath = path;
            comp.CurrentMediaStart = startedAt;
        }

        var elapsed = Math.Max(0f, (float) (_timing.CurTime - startedAt).TotalSeconds);
        _audio.SetPlaybackPosition(comp.SoundEntity, elapsed);
        comp.NextResync = _timing.CurTime + ResyncInterval;

        var gain = _power.IsPowered(uid) && comp.Active
            ? comp.DefaultParams.Volume
            : 0f;
        _audio.SetGain(comp.SoundEntity, gain);
    }

    private bool TryGetBroadcast(uint frequency, out string path, out TimeSpan startedAt)
    {
        if (_broadcastCache.TryGetValue(frequency, out var broadcast))
        {
            startedAt = broadcast.StartedAt;
            path = broadcast.Path;
            return true;
        }

        startedAt = default;
        path = string.Empty;
        return false;
    }

    private void RefreshBroadcastCache(bool force)
    {
        if (!force && _nextBroadcastCacheRefresh > _timing.CurTime)
            return;

        _broadcastCache.Clear();

        var query = EntityQueryEnumerator<StationRadioServerComponent>();
        while (query.MoveNext(out _, out var server))
        {
            if (!server.Broadcasting || server.CurrentMediaPath == null || server.CurrentMediaStart == null)
                continue;

            if (_broadcastCache.TryGetValue(server.Frequency, out var current)
                && current.StartedAt >= server.CurrentMediaStart.Value)
            {
                continue;
            }

            _broadcastCache[server.Frequency] = (server.CurrentMediaPath, server.CurrentMediaStart.Value);
        }

        _nextBroadcastCacheRefresh = _timing.CurTime + BroadcastCacheInterval;
    }

    private void StopReceiverSound(StationRadioReceiverComponent comp)
    {
        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
        comp.CurrentMediaPath = null;
        comp.CurrentMediaStart = null;
        comp.NextResync = TimeSpan.Zero;
    }
}
