using System.Linq;
using System.Numerics;
using Content.Server._Metro14.SpecialAbilities;
using Content.Shared._Metro14.SpecialAbilities;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Server._Metro14.SpecialAbilities;

public sealed class SpecialAbilitiesSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly SharedStunSystem _stuns = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecialAbilitiesComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SpecialAbilitiesComponent, SpecialSleepActionEvent>(OnSleepAction);
        SubscribeLocalEvent<SpecialAbilitiesComponent, SpecialKnockbackActionEvent>(OnKnockbackAction);
        SubscribeLocalEvent<SpecialAbilitiesComponent, SpecialShaderActionEvent>(OnShaderAction);
        SubscribeLocalEvent<SpecialAbilitiesComponent, SpecialInvisibilityActionEvent>(OnInvisibilityAction);
        SubscribeLocalEvent<SpecialAbilitiesComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, SpecialAbilitiesComponent component, ComponentInit args)
    {
        TrySetAction(uid, component.SleepAction, component.SleepActionEntity);
        TrySetAction(uid, component.KnockbackAction, component.KnockbackActionEntity);
        TrySetAction(uid, component.ShaderAction, component.ShaderActionEntity);
        TrySetAction(uid, component.InvisibilityAction, component.InvisibilityActionEntity);

        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 1.0f, stealth);
        _stealth.SetEnabled(uid, false, stealth);
        Dirty(uid, stealth);
    }

    private void OnSleepAction(EntityUid uid, SpecialAbilitiesComponent component, SpecialSleepActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var userPos = _transform.GetMapCoordinates(uid);

        var entities = _lookup.GetEntitiesInRange<MobStateComponent>(userPos, component.SleepRadius);

        foreach (var entity in entities)
        {
            if (entity.Owner == uid || HasComp<SpecialAbilitiesComponent>(entity.Owner))
                continue;

            if (TryComp<MobStateComponent>(entity.Owner, out var mobState))
            {
                _statusEffects.TryAddStatusEffectDuration(entity.Owner, SleepingSystem.StatusEffectForcedSleeping, component.SleepDuration);
            }
        }
    }

    private void OnKnockbackAction(EntityUid uid, SpecialAbilitiesComponent component, SpecialKnockbackActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var userPos = _transform.GetMapCoordinates(uid);

        var entities = _lookup.GetEntitiesInRange<MobStateComponent>(userPos, component.KnockbackRadius);

        foreach (var entity in entities)
        {
            if (entity.Owner == uid || HasComp<SpecialAbilitiesComponent>(entity.Owner))
                continue;

            var direction = _transform.GetMapCoordinates(entity.Owner).Position - userPos.Position;
            direction = direction.Normalized();

            _stuns.TryCrawling(entity.Owner, TimeSpan.FromSeconds(3));
            _throwing.TryThrow(entity.Owner, direction, component.KnockbackForce, uid, compensateFriction: false);
        }
    }

    private void OnShaderAction(EntityUid uid, SpecialAbilitiesComponent component, SpecialShaderActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var userPos = _transform.GetMapCoordinates(uid);

        var entities = _lookup.GetEntitiesInRange<MobStateComponent>(userPos, component.ShaderRadius);

        foreach (var entity in entities)
        {
            if (entity.Owner == uid || HasComp<SpecialAbilitiesComponent>(entity.Owner))
                continue;

            RaiseNetworkEvent(new ApplyHallucinationShaderEvent(
                GetNetEntity(entity.Owner),
                component.ShaderDuration
            ));
        }
    }

    private void OnInvisibilityAction(EntityUid uid, SpecialAbilitiesComponent component, SpecialInvisibilityActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var stealth = EnsureComp<StealthComponent>(uid);
        bool newState = !stealth.Enabled;

        if (newState)
        {
            _stealth.SetVisibility(uid, 0.001f, stealth);
            _stealth.SetEnabled(uid, true, stealth);
        }
        else
        {
            _stealth.SetEnabled(uid, false, stealth);
        }

        if (component.InvisibilityActionEntity != null)
        {
            _actionsSystem.SetToggled(component.InvisibilityActionEntity.Value, newState);
        }
    }

    private void OnRemove(EntityUid uid, SpecialAbilitiesComponent component, ComponentRemove args)
    {
        if (component.SleepActionEntity != null)
            _actionsSystem.RemoveAction(component.SleepActionEntity.Value);
        if (component.KnockbackActionEntity != null)
            _actionsSystem.RemoveAction(component.KnockbackActionEntity.Value);
        if (component.ShaderActionEntity != null)
            _actionsSystem.RemoveAction(component.ShaderActionEntity.Value);
        if (component.InvisibilityActionEntity != null)
            _actionsSystem.RemoveAction(component.InvisibilityActionEntity.Value);
    }

    private void TrySetAction(EntityUid uid, EntProtoId actionProtoId, EntityUid? actionEntityUid)
    {
        actionEntityUid = _actionsSystem.AddAction(uid, actionProtoId);

        if (actionEntityUid != null)
        {
            _actionsSystem.StartUseDelay(actionEntityUid.Value);
        }
    }
}