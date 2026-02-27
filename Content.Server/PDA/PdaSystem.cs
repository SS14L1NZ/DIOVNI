using Content.Server.Access.Systems;
using Content.Server.AlertLevel;
using Content.Server.CartridgeLoader;
using Content.Server.Chat.Managers;
using Content.Server.Instruments;
using Content.Server.PDA.Ringer;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Server.Traitor.Uplink;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.Chat;
using Content.Shared.Light;
using Content.Shared.Light.EntitySystems;
using Content.Shared.PDA;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Shared._NF.Bank.Components; // Frontier
using Content.Shared._NF.Shipyard.Components; // Frontier
using Content.Server._NF.Shipyard.Systems; // Frontier
using Content.Server._NF.SectorServices; // Frontier
using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Content.Shared.DeviceNetwork.Components;
using Content.Server.Radio.Components;
using Content.Shared._Goobstation.StationRadio.Components;
using Content.Shared._Goobstation.StationRadio.Events;
using Robust.Shared.Timing;

namespace Content.Server.PDA
{
    public sealed class PdaSystem : SharedPdaSystem
    {
        [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
        [Dependency] private readonly InstrumentSystem _instrument = default!;
        [Dependency] private readonly RingerSystem _ringer = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly StoreSystem _store = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly UnpoweredFlashlightSystem _unpoweredFlashlight = default!;
        [Dependency] private readonly ContainerSystem _containerSystem = default!;
        [Dependency] private readonly IdCardSystem _idCard = default!;
        [Dependency] private readonly SectorServiceSystem _sectorService = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        private static readonly TimeSpan StationRadioScanDuration = TimeSpan.FromSeconds(2);
        private static readonly List<PdaStationRadioScanEntry> EmptyStationRadioScanResults = new();
        private readonly Dictionary<EntityUid, TimeSpan> _pendingStationRadioScans = new();
        private readonly Dictionary<EntityUid, List<PdaStationRadioScanEntry>> _stationRadioScanResults = new();
        private readonly List<EntityUid> _completedStationRadioScans = new();

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PdaComponent, LightToggleEvent>(OnLightToggle);

            // UI Events:
            SubscribeLocalEvent<PdaComponent, BoundUIOpenedEvent>(OnPdaOpen);
            SubscribeLocalEvent<PdaComponent, PdaRequestUpdateInterfaceMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaToggleFlashlightMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaShowRingtoneMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaShowMusicMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaToggleStationRadioMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaSelectStationRadioFrequencyMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaScanStationRadioMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaShowUplinkMessage>(OnUiMessage);
            SubscribeLocalEvent<PdaComponent, PdaLockUplinkMessage>(OnUiMessage);

            SubscribeLocalEvent<PdaComponent, CartridgeLoaderNotificationSentEvent>(OnNotification);
            SubscribeLocalEvent<PdaComponent, EntityTerminatingEvent>(OnPdaTerminating);

            SubscribeLocalEvent<StationRenamedEvent>(OnStationRenamed);
            SubscribeLocalEvent<EntityRenamedEvent>(OnEntityRenamed, after: new[] { typeof(IdCardSystem) });
            SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_pendingStationRadioScans.Count == 0)
                return;

            _completedStationRadioScans.Clear();
            foreach (var (uid, completeTime) in _pendingStationRadioScans)
            {
                if (completeTime > _timing.CurTime)
                    continue;

                _completedStationRadioScans.Add(uid);
            }

            foreach (var uid in _completedStationRadioScans)
            {
                _pendingStationRadioScans.Remove(uid);
                _stationRadioScanResults[uid] = BuildStationRadioScanResults();

                if (TryComp(uid, out PdaComponent? pda))
                    UpdatePdaUi(uid, pda);
            }
        }

        private void OnEntityRenamed(ref EntityRenamedEvent ev)
        {
            if (HasComp<IdCardComponent>(ev.Uid))
                return;

            if (_idCard.TryFindIdCard(ev.Uid, out var idCard))
            {
                var query = EntityQueryEnumerator<PdaComponent>();

                while (query.MoveNext(out var uid, out var comp))
                {
                    if (comp.ContainedId == idCard)
                    {
                        SetOwner(uid, comp, ev.Uid, ev.NewName);
                    }
                }
            }
        }

        protected override void OnComponentInit(EntityUid uid, PdaComponent pda, ComponentInit args)
        {
            base.OnComponentInit(uid, pda, args);

            if (!HasComp<UserInterfaceComponent>(uid))
                return;

            UpdateAlertLevel(uid, pda);
            UpdateStationName(uid, pda);
        }

        protected override void OnItemInserted(EntityUid uid, PdaComponent pda, EntInsertedIntoContainerMessage args)
        {
            base.OnItemInserted(uid, pda, args);
            var id = CompOrNull<IdCardComponent>(pda.ContainedId);
            if (id != null)
                pda.OwnerName = id.FullName;
            UpdatePdaUi(uid, pda);
        }

        protected override void OnItemRemoved(EntityUid uid, PdaComponent pda, EntRemovedFromContainerMessage args)
        {
            if (args.Container.ID != pda.IdSlot.ID && args.Container.ID != pda.PenSlot.ID && args.Container.ID != pda.PaiSlot.ID && args.Container.ID != pda.BookSlot.ID)
                return;

            // TODO: This is super cursed just use compstates please.
            if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
                return;

            base.OnItemRemoved(uid, pda, args);
            UpdatePdaUi(uid, pda);
        }

        private void OnLightToggle(EntityUid uid, PdaComponent pda, LightToggleEvent args)
        {
            pda.FlashlightOn = args.IsOn;
            UpdatePdaUi(uid, pda);
        }

        public void SetOwner(EntityUid uid, PdaComponent pda, EntityUid owner, string ownerName)
        {
            pda.OwnerName = ownerName;
            pda.PdaOwner = owner;
            UpdatePdaUi(uid, pda);
        }

        private void OnStationRenamed(StationRenamedEvent ev)
        {
            UpdateAllPdaUisOnStation();
        }

        private void OnAlertLevelChanged(AlertLevelChangedEvent args)
        {
            UpdateAllPdaUisOnStation();
        }

        private void UpdateAllPdaUisOnStation()
        {
            var query = AllEntityQuery<PdaComponent>();
            while (query.MoveNext(out var ent, out var comp))
            {
                UpdatePdaUi(ent, comp);
            }
        }

        private void OnNotification(Entity<PdaComponent> ent, ref CartridgeLoaderNotificationSentEvent args)
        {
            _ringer.RingerPlayRingtone(ent.Owner);

            if (!_containerSystem.TryGetContainingContainer((ent, null, null), out var container)
                || !TryComp<ActorComponent>(container.Owner, out var actor))
                return;

            var message = FormattedMessage.EscapeText(args.Message);
            var wrappedMessage = Loc.GetString("pda-notification-message",
                ("header", args.Header),
                ("message", message));

            _chatManager.ChatMessageToOne(
                ChatChannel.Notifications,
                message,
                wrappedMessage,
                EntityUid.Invalid,
                false,
                actor.PlayerSession.Channel);
        }

            private void OnPdaTerminating(EntityUid uid, PdaComponent component, ref EntityTerminatingEvent args)
            {
                _pendingStationRadioScans.Remove(uid);
                _stationRadioScanResults.Remove(uid);
            }

        /// <summary>
        /// Send new UI state to clients, call if you modify something like uplink.
        /// </summary>
        public void UpdatePdaUi(EntityUid uid, PdaComponent? pda = null, EntityUid? actor_uid = null) // Frontier
        {
            if (!Resolve(uid, ref pda, false))
                return;

            if (!_ui.HasUi(uid, PdaUiKey.Key))
                return;

            var address = GetDeviceNetAddress(uid);
            var hasInstrument = HasComp<InstrumentComponent>(uid);
            var canListenStationRadio = TryComp<StationRadioReceiverComponent>(uid, out var stationRadioComp);
            var stationRadioEnabled = stationRadioComp?.Active ?? false;
            var stationRadioFrequency = stationRadioComp?.Frequency ?? 145;
            var stationRadioName = stationRadioComp == null
                ? null
                : GetStationRadioNameForFrequency(stationRadioComp.Frequency);
            var stationRadioScanning = _pendingStationRadioScans.ContainsKey(uid);
            var stationRadioScanResults = _stationRadioScanResults.GetValueOrDefault(uid) ?? EmptyStationRadioScanResults;
            var showUplink = HasComp<UplinkComponent>(uid) && IsUnlocked(uid);

            UpdateStationName(uid, pda);
            UpdateAlertLevel(uid, pda);
            // TODO: Update the level and name of the station with each call to UpdatePdaUi is only needed for latejoin players.
            // TODO: If someone can implement changing the level and name of the station when changing the PDA grid, this can be removed.

            // TODO don't make this depend on cartridge loader!?!?
            if (!TryComp(uid, out CartridgeLoaderComponent? loader))
                return;

            var programs = _cartridgeLoader.GetAvailablePrograms(uid, loader);
            var id = CompOrNull<IdCardComponent>(pda.ContainedId);
            var balance = 0; // frontier
            if (actor_uid != null && TryComp<BankAccountComponent>(actor_uid, out var account)) // frontier
                balance = account.Balance; // frontier
            var ownedShipName = ""; // Frontier
            if (TryComp<ShuttleDeedComponent>(pda.ContainedId, out var shuttleDeedComp)) // Frontier
                ownedShipName = ShipyardSystem.GetFullName(shuttleDeedComp); // Frontier

            // Get company information from ID card
            string? companyName = null;
            Color companyColor = Color.White;
            if (id?.CompanyName != null && !string.IsNullOrWhiteSpace(id.CompanyName) && id.CompanyName != "None")
            {
                if (_prototypeManager.TryIndex<CompanyPrototype>(id.CompanyName, out var companyProto))
                {
                    companyName = companyProto.Name; // Use the display name, not the ID
                    companyColor = companyProto.Color;
                }
                else
                {
                    // Fallback to ID if prototype not found
                    companyName = id.CompanyName;
                }
            }

            var state = new PdaUpdateState(
                programs,
                GetNetEntity(loader.ActiveProgram),
                pda.FlashlightOn,
                pda.PenSlot.HasItem,
                pda.PaiSlot.HasItem,
                pda.BookSlot.HasItem,
                new PdaIdInfoText
                {
                    ActualOwnerName = pda.OwnerName,
                    IdOwner = id?.FullName,
                    JobTitle = id?.LocalizedJobTitle,
                    CompanyName = companyName,
                    CompanyColor = companyColor,
                    StationAlertLevel = pda.StationAlertLevel,
                    StationAlertColor = pda.StationAlertColor
                },
                balance, // Frontier
                ownedShipName, // Frontier
                pda.StationName,
                showUplink,
                hasInstrument,
                canListenStationRadio,
                stationRadioEnabled,
                stationRadioFrequency,
                stationRadioName,
                stationRadioScanning,
                stationRadioScanResults,
                address);

            _ui.SetUiState(uid, PdaUiKey.Key, state);
        }

        private void OnPdaOpen(Entity<PdaComponent> ent, ref BoundUIOpenedEvent args)
        {
            if (!PdaUiKey.Key.Equals(args.UiKey))
                return;

            UpdatePdaUi(ent.Owner, ent.Comp, args.Actor); // Frontier
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaRequestUpdateInterfaceMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            UpdatePdaUi(uid, pda);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaToggleFlashlightMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            // TODO PREDICTION
            // When moving this to shared, fill in the user field
            _unpoweredFlashlight.TryToggleLight(uid, user: null);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaShowRingtoneMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            if (HasComp<RingerComponent>(uid))
                _ringer.ToggleRingerUI(uid, msg.Actor);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaShowMusicMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            if (TryComp<InstrumentComponent>(uid, out var instrument))
                _instrument.ToggleInstrumentUi(uid, msg.Actor, instrument);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaToggleStationRadioMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            if (!TryComp<StationRadioReceiverComponent>(uid, out var stationRadio))
                return;

            stationRadio.Active = !stationRadio.Active;
            RaiseLocalEvent(uid, new StationRadioRefreshEvent());
            UpdatePdaUi(uid, pda, msg.Actor);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaSelectStationRadioFrequencyMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            if (!TryComp<StationRadioReceiverComponent>(uid, out var stationRadio))
                return;

            if (msg.Frequency is < 1 or > 9999)
            {
                UpdatePdaUi(uid, pda, msg.Actor);
                return;
            }

            stationRadio.Frequency = (uint) msg.Frequency;
            RaiseLocalEvent(uid, new StationRadioRefreshEvent());
            UpdatePdaUi(uid, pda, msg.Actor);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaScanStationRadioMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            if (!HasComp<StationRadioReceiverComponent>(uid))
                return;

            _pendingStationRadioScans[uid] = _timing.CurTime + StationRadioScanDuration;
            UpdatePdaUi(uid, pda, msg.Actor);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaShowUplinkMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            // check if its locked again to prevent malicious clients opening locked uplinks
            if (HasComp<UplinkComponent>(uid) && IsUnlocked(uid))
                _store.ToggleUi(msg.Actor, uid);
        }

        private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaLockUplinkMessage msg)
        {
            if (!PdaUiKey.Key.Equals(msg.UiKey))
                return;

            if (TryComp<RingerUplinkComponent>(uid, out var uplink))
            {
                _ringer.LockUplink(uid, uplink);
                UpdatePdaUi(uid, pda);
            }
        }

        private bool IsUnlocked(EntityUid uid)
        {
            return !TryComp<RingerUplinkComponent>(uid, out var uplink) || uplink.Unlocked;
        }

        private void UpdateStationName(EntityUid uid, PdaComponent pda)
        {
            var station = _station.GetOwningStation(uid);
            pda.StationName = station is null ? null : Name(station.Value);
        }

        private void UpdateAlertLevel(EntityUid uid, PdaComponent pda)
        {
            //var station = _station.GetOwningStation(uid); // Frontier
            var station = _sectorService.GetServiceEntity(); // Frontier
            if (!TryComp(station, out AlertLevelComponent? alertComp) ||
                alertComp.AlertLevels == null)
                return;
            pda.StationAlertLevel = alertComp.CurrentLevel;
            if (alertComp.AlertLevels.Levels.TryGetValue(alertComp.CurrentLevel, out var details))
                pda.StationAlertColor = details.Color;
        }

        private string? GetDeviceNetAddress(EntityUid uid)
        {
            string? address = null;

            if (TryComp(uid, out DeviceNetworkComponent? deviceNetworkComponent))
            {
                address = deviceNetworkComponent?.Address;
            }

            return address;
        }

        private string? GetStationRadioNameForFrequency(uint frequency)
        {
            string? name = null;

            var query = EntityQueryEnumerator<StationRadioServerComponent, RadioMicrophoneComponent>();
            while (query.MoveNext(out _, out var server, out var microphone))
            {
                var isAvailable = server.Broadcasting || microphone.Enabled;
                if (!isAvailable || server.Frequency != frequency)
                    continue;

                name = string.IsNullOrWhiteSpace(server.BroadcastName)
                    ? "Station Radio"
                    : server.BroadcastName;
                break;
            }

            return name;
        }

        private List<PdaStationRadioScanEntry> BuildStationRadioScanResults()
        {
            var seen = new HashSet<uint>();
            var results = new List<PdaStationRadioScanEntry>();

            var query = EntityQueryEnumerator<StationRadioServerComponent, RadioMicrophoneComponent>();
            while (query.MoveNext(out _, out var server, out var microphone))
            {
                var isAvailable = server.Broadcasting || microphone.Enabled;
                if (!isAvailable)
                    continue;

                if (!seen.Add(server.Frequency))
                    continue;

                var name = string.IsNullOrWhiteSpace(server.BroadcastName)
                    ? "Station Radio"
                    : server.BroadcastName;

                results.Add(new PdaStationRadioScanEntry(server.Frequency, name));
            }

            results.Sort((left, right) => left.Frequency.CompareTo(right.Frequency));
            return results;
        }
    }
}
