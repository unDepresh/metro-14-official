using System.Numerics;
using Content.Client._Metro14.SpecialAbilities;
using Content.Shared._Metro14.SpecialAbilities;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Metro14.SpecialAbilities;

public sealed class SpecialAbilitiesClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private HallucinationsOverlay _overlay = default!;
    private readonly Dictionary<EntityUid, TimeSpan> _hallucinationEffects = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();
        SubscribeNetworkEvent<ApplyHallucinationShaderEvent>(OnApplyHallucinationShader);
    }

    private void OnApplyHallucinationShader(ApplyHallucinationShaderEvent ev)
    {
        var target = GetEntity(ev.Target);

        if (_playerManager.LocalEntity != target)
            return;

        var endTime = _timing.CurTime + ev.Duration;
        _hallucinationEffects[target] = endTime;

        if (!_overlayManager.HasOverlay<HallucinationsOverlay>())
        {
            _overlay = new HallucinationsOverlay();
            _overlayManager.AddOverlay(_overlay);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var expired = new List<EntityUid>();

        foreach (var (uid, endTime) in _hallucinationEffects)
        {
            if (curTime >= endTime)
            {
                expired.Add(uid);
            }
        }

        foreach (var uid in expired)
        {
            _hallucinationEffects.Remove(uid);
        }

        if (_hallucinationEffects.Count == 0 && _overlayManager.HasOverlay<HallucinationsOverlay>())
        {
            _overlayManager.RemoveOverlay(_overlay);
        }
    }
}