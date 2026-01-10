using Content.Server.Database;
using Content.Shared.Ghost;
using Robust.Shared.Player;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Asynchronous;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Server._Metro14.SponsorSystem.GhostSkinSystem;

namespace Content.Server._Metro14.SponsorSystem.GhostSkinSystem;

public sealed class SponsorGhostSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CanBeSponsorGhostComponent, MindAddedMessage>(OnComponentInit);
        SubscribeLocalEvent<CanBeSponsorGhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private async void OnComponentInit(EntityUid uid, CanBeSponsorGhostComponent component, MindAddedMessage args)
    {
        if (!TryComp<GhostComponent>(uid, out var ghost)) // защита от дураков, которые решат выдать данный компонент другим сущностям.
            return;

        if (!TryComp<MindContainerComponent>(uid, out var mindContainer))
            return;

        if (mindContainer.Mind == null)
            return;

        var mindId = mindContainer.Mind.Value;

        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;

        var userId = mind.UserId.Value;

        var isSponsor = await _dbManager.IsSponsorAsync(userId);

        if (!isSponsor)
        {
            SetGhostSprite(uid, component.DefaultState);
            return;
        }

        var sponsorInfo = await _dbManager.GetSponsorInfoAsync(userId);

        if (sponsorInfo == null || !sponsorInfo.IsActive)
        {
            SetGhostSprite(uid, component.DefaultState);
            return;
        }

        var spriteState = component.SponsorStates.GetValueOrDefault(
            sponsorInfo.Tier.ToLower(),
            component.DefaultState
        );

        SetGhostSprite(uid, spriteState);
    }

    private async void OnPlayerAttached(EntityUid uid, CanBeSponsorGhostComponent component, PlayerAttachedEvent args)
    {
        // Проверяем, что присоединенная сущность - призрак с нашим компонентом  
        if (args.Entity != uid || !HasComp<GhostComponent>(uid))
            return;

        if (!TryComp<VisitingMindComponent>(uid, out var mindContainer))
            return;

        if (mindContainer.MindId == null)
            return;

        var mindId = mindContainer.MindId.Value;

        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;

        var userId = mind.UserId.Value;

        var isSponsor = await _dbManager.IsSponsorAsync(userId);

        if (!isSponsor)
        {
            SetGhostSprite(uid, component.DefaultState);
            return;
        }

        var sponsorInfo = await _dbManager.GetSponsorInfoAsync(userId);

        if (sponsorInfo == null || !sponsorInfo.IsActive)
        {
            SetGhostSprite(uid, component.DefaultState);
            return;
        }

        var spriteState = component.SponsorStates.GetValueOrDefault(
            sponsorInfo.Tier.ToLower(),
            component.DefaultState
        );

        SetGhostSprite(uid, spriteState);
    }

    private void SetGhostSprite(EntityUid uid, string spriteState)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, GhostVisuals.SpriteState, spriteState, appearance);
    }
}
