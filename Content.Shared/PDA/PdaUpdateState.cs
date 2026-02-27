using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.PDA
{
    [Serializable, NetSerializable]
    public sealed class PdaUpdateState : CartridgeLoaderUiState // WTF is this. what. I ... fuck me I just want net entities to work
        // TODO purge this shit
        //AAAAAAAAAAAAAAAA
    {
        public bool FlashlightEnabled;
        public bool HasPen;
        public bool HasPai;
        public bool HasBook;
        public PdaIdInfoText PdaOwnerInfo;
        public string? StationName;
        public bool HasUplink;
        public bool CanPlayMusic;
        public bool CanListenStationRadio;
        public bool StationRadioEnabled;
        public uint StationRadioFrequency;
        public string? StationRadioName;
        public bool StationRadioScanning;
        public List<PdaStationRadioScanEntry> StationRadioScanResults;
        public string? Address;
        public int Balance; // Frontier
        public string? OwnedShipName; // Frontier

        public PdaUpdateState(
            List<NetEntity> programs,
            NetEntity? activeUI,
            bool flashlightEnabled,
            bool hasPen,
            bool hasPai,
            bool hasBook,
            PdaIdInfoText pdaOwnerInfo,
            int balance, // Frontier
            string? ownedShipName, // Frontier
            string? stationName,
            bool hasUplink = false,
            bool canPlayMusic = false,
            bool canListenStationRadio = false,
            bool stationRadioEnabled = false,
            uint stationRadioFrequency = 145,
            string? stationRadioName = null,
            bool stationRadioScanning = false,
            List<PdaStationRadioScanEntry>? stationRadioScanResults = null,
            string? address = null)
            : base(programs, activeUI)
        {
            FlashlightEnabled = flashlightEnabled;
            HasPen = hasPen;
            HasPai = hasPai;
            HasBook = hasBook;
            PdaOwnerInfo = pdaOwnerInfo;
            HasUplink = hasUplink;
            CanPlayMusic = canPlayMusic;
            CanListenStationRadio = canListenStationRadio;
            StationRadioEnabled = stationRadioEnabled;
            StationRadioFrequency = stationRadioFrequency;
            StationRadioName = stationRadioName;
            StationRadioScanning = stationRadioScanning;
            StationRadioScanResults = stationRadioScanResults ?? new List<PdaStationRadioScanEntry>();
            StationName = stationName;
            Address = address;
            Balance = balance; // Frontier
            OwnedShipName = ownedShipName; // Frontier
        }
    }

    [Serializable, NetSerializable]
    public struct PdaIdInfoText
    {
        public string? ActualOwnerName;
        public string? IdOwner;
        public string? JobTitle;
        public string? CompanyName;
        public Color CompanyColor;
        public string? StationAlertLevel;
        public Color StationAlertColor;
    }

    [Serializable, NetSerializable]
    public sealed class PdaStationRadioScanEntry
    {
        public uint Frequency;
        public string Name;

        public PdaStationRadioScanEntry(uint frequency, string name)
        {
            Frequency = frequency;
            Name = name;
        }
    }
}
