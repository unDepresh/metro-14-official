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
using Content.Shared.Actions;

namespace Content.Server._Metro14.SponsorSystem.GhostSkinSystem;

public sealed class SponsorGhostSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CanBeSponsorGhostComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CanBeSponsorGhostComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<CanBeSponsorGhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CanBeSponsorGhostComponent, TryChangeGhostSkinActionEvent>(OnTryChangeGhostSkinActionPressed);
    }

    private void OnComponentInit(EntityUid uid, CanBeSponsorGhostComponent component, ComponentInit args)
    {
        TrySetAction(uid, component.TryChangeGhostSkinAction, ref component.TryChangeGhostSkinActionEntity);
    }

    private async void OnMindAdded(EntityUid uid, CanBeSponsorGhostComponent component, MindAddedMessage args)
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

        int indexSprite = component.SponsorsRankStates.IndexOf(spriteState);
        if (indexSprite >= 0)
        {
            component.CurrentIndex = indexSprite;
            for (int i = 0; i <= indexSprite; i++)
            {
                component.AvailableStates.Add(component.SponsorsRankStates[i]);
            }
        }

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

        int indexSprite = component.SponsorsRankStates.IndexOf(spriteState);
        if (indexSprite >= 0)
        {
            component.CurrentIndex = indexSprite;
            for (int i = 0; i <= indexSprite; i++)
            {
                component.AvailableStates.Add(component.SponsorsRankStates[i]);
            }
        }

        SetGhostSprite(uid, spriteState);
    }

    /// <summary>
    /// Вспомогательный метод для установки нужного спрайта призраку.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="spriteState"></param>
    private void SetGhostSprite(EntityUid uid, string spriteState)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, GhostVisuals.SpriteState, spriteState, appearance);
    }

    /// <summary>
    /// Обработчик ивента, поднимаемого при нажатии кнопки для смены скина наблюдателя.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void OnTryChangeGhostSkinActionPressed(EntityUid uid, CanBeSponsorGhostComponent component, TryChangeGhostSkinActionEvent args)
    {
        if (!TryComp<GhostComponent>(uid, out var ghost))
            return;

        if (component.TryChangeGhostSkinActionEntity == null)
            return;

        component.CurrentIndex = ChangeIndex(component.CurrentIndex, component.AvailableStates.Count);
        var spriteState = component.SponsorsRankStates[component.CurrentIndex];

        SetGhostSprite(uid, spriteState);

        _actionsSystem.StartUseDelay(component.TryChangeGhostSkinActionEntity.Value);
    }

    /// <summary>
    /// Вспомогательный метод для безопасной смены индекса скина.
    /// </summary>
    /// <param name="currentIndex"></param>
    /// <param name="maxIndex"></param>
    /// <returns></returns>
    private int ChangeIndex(int currentIndex, int maxIndex)
    {
        return currentIndex + 1 >= maxIndex ? 0 : currentIndex + 1;
    }

    /// <summary>
    /// Вспомогательный метод для установки действия смены скина наблюдателя.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="actionProtoId"></param>
    /// <param name="actionEntityUid"></param>
    private void TrySetAction(EntityUid uid, EntProtoId actionProtoId, ref EntityUid? actionEntityUid)
    {
        actionEntityUid = _actionsSystem.AddAction(uid, actionProtoId);

        if (actionEntityUid != null)
        {
            _actionsSystem.StartUseDelay(actionEntityUid.Value);
        }
    }
}
