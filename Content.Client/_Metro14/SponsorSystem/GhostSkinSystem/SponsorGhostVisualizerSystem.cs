using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Content.Server._Metro14.SponsorSystem.GhostSkinSystem;

namespace Content.Client._Metro14.SponsorSystem.GhostSkinSystem;

public sealed class SponsorGhostVisualizerSystem : VisualizerSystem<CanBeSponsorGhostComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, CanBeSponsorGhostComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, GhostVisuals.SpriteState, out var spriteState, args.Component))
            return;

        var layer = _sprite.LayerMapGet((uid, args.Sprite), GhostVisualLayers.Base);
        _sprite.LayerSetRsiState((uid, args.Sprite), layer, spriteState);
    }
}
