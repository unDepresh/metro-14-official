using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared._Metro14.KeyDoor;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;

namespace Content.Server._Metro14.KeyDoor;

public sealed class KeyDoorSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedHandsSystem _handSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly OccluderSystem _occluder = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<KeyDoorComponent, InteractHandEvent>(OnInteractHandEvent);
    }

    private void OnInteractHandEvent(EntityUid uid, KeyDoorComponent component, InteractHandEvent args)
    {
        if (args.Handled || args.Target == null || args.User == null)
            return;

        var door = args.Target;
        var user = args.User;

        if (!TryComp<KeyDoorComponent>(door, out var keyDoorComponent))
            return;

        if (keyDoorComponent.Access.Count == 0)
        {
            ChangeDoorState(uid, keyDoorComponent);
            return;
        }

        var keyForDoorComponent = TryFindKey(user);
        if (keyForDoorComponent == null || keyForDoorComponent.AccessList.Count == 0)
            return;

        foreach (string keyAccess in keyForDoorComponent.AccessList)
        {
            if (keyDoorComponent.Access.Contains(keyAccess))
            {
                ChangeDoorState(uid, keyDoorComponent);
                return;
            }
        }

        args.Handled = true;
    }

    private void ChangeDoorState(EntityUid uid, KeyDoorComponent keyDoorComponent)
    {
        if (keyDoorComponent.IsOpen)
        {
            keyDoorComponent.IsOpen = false;
            SetDoorSprite(uid, keyDoorComponent.CloseSpriteState);

            _physics.SetCanCollide(uid, true);

            if (keyDoorComponent.Occluder)
                _occluder.SetEnabled(uid, true);

            _audio.PlayPvs(keyDoorComponent.CloseSound, uid);
        }
        else
        {
            keyDoorComponent.IsOpen = true;
            SetDoorSprite(uid, keyDoorComponent.OpenSpriteState);

            _physics.SetCanCollide(uid, false);

            if (keyDoorComponent.Occluder)
                _occluder.SetEnabled(uid, false);

            _audio.PlayPvs(keyDoorComponent.OpenSound, uid);
        }
    }



    /// <summary>
    /// Метод для поиска ключа у игрока.
    /// </summary>
    public KeyForDoorComponent? TryFindKey(EntityUid user)
    {
        // Проверяем руки на наличие предмета
        if (_entityManager.TryGetComponent(user, out HandsComponent? handsComponent))
        {
            foreach (var hand in handsComponent.Hands.Keys)
            {
                var tempHoldItem = _handSystem.GetHeldItem(user, hand);

                if (tempHoldItem != null)
                {
                    if (TryComp<KeyForDoorComponent>((EntityUid) tempHoldItem, out var keyForDoorComponent))
                        return keyForDoorComponent;
                }
            }
        }

        // теперь ищем в карманах, на поясе, спине или в рюкзаке
        var slotEnumerator = _inventory.GetSlotEnumerator(user);
        while (slotEnumerator.NextItem(out var item, out var slot))
        {
            if (!_entityManager.TryGetComponent(item, out StorageComponent? storageComponent))
            {
                if (TryComp<KeyForDoorComponent>((EntityUid) item, out var keyForDoorComp))
                    return keyForDoorComp;
            }
            else
            {
                if (storageComponent == null)
                    continue;

                return TryFindKeyInStorage(storageComponent);
            }
        }
        return null;
    }

    /// <summary>
    /// Вспомагательный метод для рекурсивного поиска ключа в рюкзаке.
    /// </summary>
    private KeyForDoorComponent? TryFindKeyInStorage(StorageComponent storageComp)
    {
        foreach (var storageItem in storageComp.StoredItems)
        {
            if (TryComp<KeyForDoorComponent>((EntityUid) storageItem.Key, out var keyForDoorComp))
                return keyForDoorComp;

            if (_entityManager.TryGetComponent(storageItem.Key, out StorageComponent? storageComponent))
            {
                KeyForDoorComponent? theorComp = TryFindKeyInStorage(storageComponent);
                if (theorComp != null)
                    return theorComp;
            }
        }

        return null;
    }

    /// <summary>
    /// Вспомогательный метод для изменения спрайта двери при открытии/закрытии.
    /// </summary>
    private void SetDoorSprite(EntityUid uid, string spriteState)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, KeyDoorVisuals.SpriteState, spriteState, appearance);
    }
}
