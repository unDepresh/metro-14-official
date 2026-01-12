using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared._Metro14.KeyDoor;

namespace Content.Client._Metro14.KeyDoor;

public sealed class KeyDoorVisualizerSystem : VisualizerSystem<KeyDoorComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, KeyDoorComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, KeyDoorVisuals.SpriteState, out var spriteState, args.Component))
            return;

        var layer = _sprite.LayerMapGet((uid, args.Sprite), KeyDoorVisualLayers.Base);
        _sprite.LayerSetRsiState((uid, args.Sprite), layer, spriteState);
    }
}
