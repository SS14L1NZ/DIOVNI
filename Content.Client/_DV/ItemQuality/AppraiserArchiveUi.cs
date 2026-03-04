using Content.Client._DV.ItemQuality.UI;
using Content.Client.UserInterface.Fragments;
using Content.Shared._DV.ItemQuality;
using Content.Shared.CartridgeLoader;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._DV.ItemQuality;

/// <summary>
/// UIFragment for the appraiser archive PDA cartridge.
/// Bridges the BUI system to the AppraiserArchiveUiFragment control.
/// </summary>
public sealed partial class AppraiserArchiveUi : UIFragment
{
    private AppraiserArchiveUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new AppraiserArchiveUiFragment();
        _fragment.OnAction += (action, index) =>
        {
            var msg = new AppraiserArchiveUiMessageEvent(action, index);
            userInterface.SendMessage(new CartridgeUiMessage(msg));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not AppraiserArchiveUiState archiveState)
            return;

        _fragment?.UpdateState(archiveState.Entries);
    }
}
