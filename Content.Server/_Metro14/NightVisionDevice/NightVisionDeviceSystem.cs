using Content.Shared._Metro14.NightVisionDevice;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Metro14.NightVisionDevice;

public sealed class NightVisionDeviceSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionDeviceComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NightVisionDeviceComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<ToggleNightVisionDeviceActionEvent>(OnToggleNightVision);
    }

    /// <summary>
    /// Метод-обработчик нажатия кнопки-действия "Переключить ПНВ"
    /// </summary>
    /// <param name="args"> Аргументы события:
    /// Performer (EntityUid) - сущность, выполняющая действие;
    /// Action (Entity) - сущность действия;
    /// Toggle (bool) - нужно ли переключать состояние действия;
    /// </param>
    private void OnToggleNightVision(ToggleNightVisionDeviceActionEvent args)
    {
        if (args.Performer == null)
            return;

        EntityUid? nvdEntity = null;
        if (TryComp<InventoryComponent>(args.Performer, out var invComp))
        {
            foreach (var slot in invComp.Containers)
            {
                if (slot.ContainedEntity != null && HasComp<NightVisionDeviceComponent>(slot.ContainedEntity))
                {
                    nvdEntity = slot.ContainedEntity;
                    break;
                }
            }
        }

        if (nvdEntity == null)
            return;

        if (!TryComp<NightVisionDeviceComponent>((EntityUid) nvdEntity, out var nvdComp))
            return;

        nvdComp.Enabled = !nvdComp.Enabled;

        if (_playerManager.TryGetSessionByEntity(args.Performer, out var session))
        {
            if (nvdComp.Enabled) // пытаемся включить
            {
                // нет батареи -> нет заряда -> нельзя включить
                if (!_powerCell.TryGetBatteryFromSlotOrEntity(new Entity<PowerCellSlotComponent?>((EntityUid) nvdEntity, null), out var battery))
                {
                    _popup.PopupEntity(Loc.GetString("nvd-not-battery"), (EntityUid) nvdEntity);
                    nvdComp.Enabled = false;
                }
                else if (nvdComp.Wattage > _battery.GetCharge((battery.Value.Owner, battery.Value.Comp))) // если потребление прибора > имеющегося заряда, то...
                {
                    _popup.PopupEntity(Loc.GetString("nvd-low-battery"), (EntityUid) nvdEntity);
                    nvdComp.Enabled = false;
                }
                else
                {
                    RaiseNetworkEvent(new ToggleNightVisionDeviceEvent(true, GetNetEntity(nvdEntity), _audioSystem.ResolveSound(nvdComp.SoundPathActivate)), session);
                    nvdComp.Enabled = true;
                }
            }
            else
            {
                RaiseNetworkEvent(new ToggleNightVisionDeviceEvent(false, GetNetEntity(nvdEntity), _audioSystem.ResolveSound(nvdComp.SoundPathDisable)), session);
                nvdComp.Enabled = false;
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NightVisionDeviceComponent>();
        while (query.MoveNext(out var uid, out var nightVisionDeviceComponent))
        {
            if (nightVisionDeviceComponent.Enabled)
            {
                // На всякий случай проверим наличие батареи. Если ее извлекли, то выключаем...
                if (!_powerCell.TryGetBatteryFromSlotOrEntity(new Entity<PowerCellSlotComponent?>((EntityUid) uid, null), out var battery))
                {
                    TryRiseEvent(uid, false, nightVisionDeviceComponent.SoundPathDisable);
                    nightVisionDeviceComponent.Enabled = false;
                }
                else
                {
                    if (nightVisionDeviceComponent.Wattage * frameTime > _battery.GetCharge((battery.Value.Owner, battery.Value.Comp)))
                    {
                        TryRiseEvent(uid, false, nightVisionDeviceComponent.SoundPathDisable);
                        nightVisionDeviceComponent.Enabled = false;
                    }
                    else if (!_battery.TryUseCharge((EntityUid) battery.Value.Owner, nightVisionDeviceComponent.Wattage * frameTime))
                    {
                        TryRiseEvent(uid, false, nightVisionDeviceComponent.SoundPathDisable);
                        nightVisionDeviceComponent.Enabled = false;
                    }
                }

                if (_containerSystem.TryGetContainingContainer(uid, out var container))
                {
                    var wearer = container.Owner;
                    if (TryComp<MobStateComponent>(wearer, out var mobState))
                    {
                        if (_mobState.IsCritical(wearer, mobState) || _mobState.IsDead(wearer, mobState))
                        {
                            _inventorySystem.TryUnequip(wearer, "eyes", force: true);

                            TryRiseEvent(uid, false, nightVisionDeviceComponent.SoundPathDisable);
                            nightVisionDeviceComponent.Enabled = false;
                            continue;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Вспомогательный метод для передачи данных клиенту.
    /// </summary>
    /// <param name="uid"> ПНВ </param>
    /// <param name="state"> Включить/выключить шейдер </param>
    /// <param name="sound"> Звук при включении/выключении ПНВ </param>
    private void TryRiseEvent(EntityUid uid, bool state, SoundSpecifier sound)
    {
        if (_containerSystem.TryGetContainingContainer(uid, out var container))
        {
            var wearer = container.Owner;
            if (_playerManager.TryGetSessionByEntity(wearer, out var session))
            {
                RaiseNetworkEvent(new ToggleNightVisionDeviceEvent(state, GetNetEntity(uid), _audioSystem.ResolveSound(sound)), session);
            }
        }
    }

    /// <summary>
    /// Метод, который обрабатывает взаимодействие с предметом.
    /// Отслеживаем, чтобы ПНВ был экипирован в нужный слот.
    /// </summary>
    /// <param name="ent"> ПНВ </param>
    /// <param name="args"> Аргументы события </param>
    private void OnGetActions(EntityUid uid, NightVisionDeviceComponent component, ref GetItemActionsEvent args)
    {
        component.ActionEntity = null;
        // Добавляем действие только если очки надеты в слот EYES  
        if (_inventorySystem.InSlotWithFlags(uid, SlotFlags.EYES) && args.User != null)
        {
            args.AddAction(ref component.ActionEntity, component.ToggleAction);
        }
    }

    /// <summary>
    /// Метод, который вызывается при снятии экипировки.
    /// Необходимо выключить ПНВ, если он работал.
    /// </summary>
    /// <param name="uid"> ПНВ </param>
    /// <param name="component"> Компонент ПНВ </param>
    /// <param name="args"> Аргументы события </param>
    private void OnGotUnequipped(EntityUid uid, NightVisionDeviceComponent component, GotUnequippedEvent args)
    {
        var playerUid = args.Equipee;

        if (_playerManager.TryGetSessionByEntity(playerUid, out var session) && component.Enabled)
        {
            RaiseNetworkEvent(new ToggleNightVisionDeviceEvent(false, GetNetEntity(uid), _audioSystem.ResolveSound(component.SoundPathDisable)), session);
        }

        component.ActionEntity = null;
        component.Enabled = false;

    }
}
