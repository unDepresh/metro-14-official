using Content.Server.Atmos.EntitySystems;
using Content.Shared._Metro14.RadioStation.PeaceTreaty;
using Content.Shared.Atmos.Components;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._Metro14.RadioStation.PeaceTreaty;

/// <summary>
/// Система управления радиостанциями. Отслеживает состояние вышек,
/// управляет частотами фракций и обрабатывает их поражение.
/// </summary>
public sealed class PeaceTreatySystem : EntitySystem
{
    [Dependency] private readonly FlammableSystem _flammableSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SecretDocumentComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);

        SubscribeLocalEvent<SpawnPeaceTreatyEvent>(SpawnPeaceTreaty);
        SubscribeLocalEvent<TerminatePeaceTreatyEvent>(TerminatePeaceTreaty);
        SubscribeLocalEvent<SecretDocumentComponent, InteractUsingEvent>(OnInteractUsingEvent);
    }

    /// <summary>
    /// Список союзников для каждой фракции.
    /// </summary>
    public static Dictionary<string, HashSet<string>> AlliesFractions = new Dictionary<string, HashSet<string>>();

    /// <summary>
    /// Словарь для локализации частоты и наименования фракции.
    /// </summary>
    public static Dictionary<string, string> FrequenciesLocalizationMapping = new Dictionary<string, string>();

    /// <summary>
    /// Честно говоря, данный метод является немного костылем, но другого решения на данный момент у меня нет.
    /// </summary>
    private void OnMapInit(EntityUid uid, SecretDocumentComponent component, MapInitEvent args)
    {
        if (FrequenciesLocalizationMapping.Count == 0 && component.FrequenciesLocalizationMapping.Count != 0)
        {
            FrequenciesLocalizationMapping = new Dictionary<string, string>(component.FrequenciesLocalizationMapping);
        }
    }

    /// <summary>
    /// Метод, который при перезапуске раунда очищает список союзников для избежания проблем.
    /// </summary>
    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        AlliesFractions.Clear();
        FrequenciesLocalizationMapping.Clear();
    }

    /// <summary>
    /// Данный метод вызывается из класса Content.Server._Metro14.RadioStation.RadioStationSystem при поражении той или иной фракции.
    /// То есть тут выполняется удаление павшей фракции из списка союзников всех фракций, а также из глобального списка союзов.
    /// </summary>
    /// <param name="frequency"></param>
    public void RemoveDefeatedAllies(string frequency)
    {
        var secretDocuments = EntityQueryEnumerator<SecretDocumentComponent>();
        while (secretDocuments.MoveNext(out var uid, out var component))
        {
            TryRemoveAlliesFromComponent(uid, component, frequency);
        }

        TryRemoveAllies(frequency);
    }

    /// <summary>
    /// Создаем мирный договор и даем его в руки игроку, который нажал
    /// соответствующую кнопку в панели действий у секретного документа.
    /// </summary>
    /// <param name="args">
    /// User - uid пользователя, который нажал кнопку;
    /// Prototype - прототип, который нужно заспавнить при извлечении мирного договора из секретного документа;
    /// </param>
    private void SpawnPeaceTreaty(SpawnPeaceTreatyEvent args)
    {
        if (args.Prototype == null)
            return;

        var coords = Transform(GetEntity(args.User)).Coordinates;
        var item = Spawn(args.Prototype, coords);

        _handsSystem.PickupOrDrop(GetEntity(args.User), item);
    }

    /// <summary>
    /// Расторгаем мирный договор, если пользователь нажал соответствующую кнопку у секретного документа.
    /// </summary>
    /// <param name="args">
    /// SecretDocumentFrequency - частота секретного документа, у которого выбрали действие;
    /// PeaceTreatyFrequency - выбранная игроком частота для расторжения мирного договора;
    /// User - uid пользователя, который нажал кнопку;
    /// </param>
    private void TerminatePeaceTreaty(TerminatePeaceTreatyEvent args)
    {
        if (args.SecretDocumentFrequency == null)
            return;

        TryRemoveAllies(args.SecretDocumentFrequency, args.PeaceTreatyFrequency);
        TryRemoveAllies(args.PeaceTreatyFrequency, args.SecretDocumentFrequency);

        var secretDocuments = EntityQueryEnumerator<SecretDocumentComponent>();
        while (secretDocuments.MoveNext(out var uid, out var component))
        {
            // Удаляем из всех папок данной фракции. Просто я допускаю ситуацию, когда мапперы оставили более 1 секретного документа фракции на карте... Так что нужно удалить из всех.
            TryRemoveAlliesFromComponent(component, args.SecretDocumentFrequency, args.PeaceTreatyFrequency, uid);

            // Аналогичным образом для тех, с кем расторгнули мирный договор.
            TryRemoveAlliesFromComponent(component, args.PeaceTreatyFrequency, args.SecretDocumentFrequency, uid);
        }
    }

    /// <summary>
    /// Если игрок нажал мирным договором по секретным документам, то добавляем новый союз между фракциями.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void OnInteractUsingEvent(EntityUid uid, SecretDocumentComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<PeaceTreatyComponent>(args.Used, out var peaceTreatyComponent) && peaceTreatyComponent.Frequency != null)
        {
            if (component.CurrentFrequency != null)
            {
                if (string.Compare(component.CurrentFrequency, peaceTreatyComponent.Frequency) == 0)
                {
                    QueueDel(args.Used);
                    args.Handled = true;
                    return;
                }

                // Проверяем фракции на враждебность.
                if (CheckHostilityFactions(args.Used, args.User, component, peaceTreatyComponent.Frequency))
                {
                    args.Handled = true;
                    return;
                }

                var secretDocuments = EntityQueryEnumerator<SecretDocumentComponent>();
                while (secretDocuments.MoveNext(out var secretDocumentsUid, out var secDocComp))
                {
                    TryAddAlliesInComponent(secDocComp, component.CurrentFrequency, peaceTreatyComponent.Frequency);
                    TryAddAlliesInComponent(secDocComp, peaceTreatyComponent.Frequency, component.CurrentFrequency);
                }

                TryAddAllies(component.CurrentFrequency, peaceTreatyComponent.Frequency);
                TryAddAllies(peaceTreatyComponent.Frequency, component.CurrentFrequency);

                QueueDel(args.Used);
            }
        }

        args.Handled = true;
    }

    /// <summary>
    /// Проверяем, что фракции не враждуют.
    /// </summary>
    /// <param name="component"> Секретный документ, в который попытались убрать мирный договор </param>
    /// <param name="doubtfulFrequence"> Частота мирного договора </param>
    /// <returns>
    /// Если фракции враждуют, то возвращаем true, иначе - false.
    /// </returns>
    private bool CheckHostilityFactions(EntityUid peaceTreaty, EntityUid user, SecretDocumentComponent component, string doubtfulFrequence)
    {
        if (component.CannotBeAllies.Contains(doubtfulFrequence))
        {
            if (TryComp<FlammableComponent>(peaceTreaty, out var flammableComponent))
            {
                flammableComponent.FireStacks = flammableComponent.MaximumFireStacks;
                _flammableSystem.Ignite(peaceTreaty, user, flammableComponent); // Мирного договора не будет. Сжигаем его!
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Добавляем союзников в глобальный список фракций.
    /// </summary>
    /// <param name="firstFrequency"> Частота фракции, у которой появился новый союзник </param>
    /// <param name="secondFrequency"> Частота нового союзника </param>
    private void TryAddAllies(string firstFrequency, string secondFrequency)
    {
        if (AlliesFractions.ContainsKey(firstFrequency))
            AlliesFractions[firstFrequency].Add(secondFrequency);
        else
            AlliesFractions.Add(firstFrequency, new HashSet<string>() { secondFrequency });
    }

    /// <summary>
    /// Добавляем союзников в соответствующий список в компоненте секретного документа.
    /// </summary>
    /// <param name="component"> Компонент секретного документа у найденной сущности </param>
    /// <param name="needFrequency"> Частота, которой должен обладать секретный документ </param>
    /// <param name="newAlliesFrequency"> Частота нового союзника </param>
    private void TryAddAlliesInComponent(SecretDocumentComponent component, string needFrequency, string newAlliesFrequency)
    {
        if (string.Compare(component.CurrentFrequency, needFrequency) == 0)
        {
            component.AlliesFractions.Add(newAlliesFrequency);
        }
    }

    /// <summary>
    /// Удаляем союзников из глобального списка фракций.
    /// </summary>
    /// <param name="firstFrequency"> Частота фракции, у которой удаляют союзника </param>
    /// <param name="secondFrequency"> Частота союзника, с которым расторгнули мирный договор </param>
    private void TryRemoveAllies(string firstFrequency, string secondFrequency)
    {
        if (AlliesFractions.ContainsKey(firstFrequency) && AlliesFractions[firstFrequency].Contains(secondFrequency))
            AlliesFractions[firstFrequency].Remove(secondFrequency);

        RemoveEmptyAllies();
    }

    /// <summary>
    /// Вспомогательный метод для удаления пустых фракций (фракции, что не имеют союзников) из глобального списка союзов.
    /// </summary>
    private void RemoveEmptyAllies()
    {
        foreach (var allies in AlliesFractions)
        {
            if (allies.Value.Count == 0)
                AlliesFractions.Remove(allies.Key);
        }
    }

    /// <summary>
    /// Удаляем павшую фракцию из глобального списка союзников.
    /// </summary>
    /// <param name="removeFrequency"> Частота проигравшей фракции </param>
    private void TryRemoveAllies(string removeFrequency)
    {
        foreach (var allies in AlliesFractions)
        {
            if (string.Compare(allies.Key, removeFrequency) == 0)
            {
                AlliesFractions.Remove(allies.Key);
            }
            else if (allies.Value.Contains(removeFrequency))
            {
                allies.Value.Remove(removeFrequency);
            }
        }

        RemoveEmptyAllies();
    }

    /// <summary>
    /// Метод для удаления союзника из соответствующего списка в компонент секретного документа при выборе кнопки расторжения мирного договора.
    /// </summary>
    /// <param name="component"> Компонент секретного документа у найденной сущности </param>
    /// <param name="needFrequency"> Частота, которой должен обладать секретный документ </param>
    /// <param name="removeFrequency"> Частота, которую нужно удалить из списка союзников для данного секретного документа </param>
    /// <param name="user"> Пользователь, который нажал кнопку расторжения мирного договора </param>
    /// <param name="prototype"> Прототип листка мирного договора, который будет демонстративно сожжен </param>
    private void TryRemoveAlliesFromComponent(SecretDocumentComponent component, string needFrequency, string removeFrequency, EntityUid document)
    {
        if (string.Compare(component.CurrentFrequency, needFrequency) == 0)
        {
            if (component.AlliesFractions.Contains(removeFrequency))
                component.AlliesFractions.Remove(removeFrequency);

            if (component.FractionPeaceTreatyPrototype != null)
                TryIgnitePaperWhenTreatyTerminate(document, component.FractionPeaceTreatyPrototype);
        }
    }

    /// <summary>
    /// Перегрузка метода TryRemoveAlliesFromComponent для удаления павшей фракции из списка союзников.
    /// </summary>
    /// <param name="uid"> Секретный документ, который в случае соответствия частоте павшей фракции будет удален </param>
    /// <param name="component"> Компонент секретного документа </param>
    /// <param name="removeFrequency"> Частота павшей фракции </param>
    private void TryRemoveAlliesFromComponent(EntityUid uid, SecretDocumentComponent component, string removeFrequency)
    {
        if (string.Compare(component.CurrentFrequency, removeFrequency) == 0)
        {
            QueueDel(uid);
        }
        else
        {
            if (component.AlliesFractions.Contains(removeFrequency))
                component.AlliesFractions.Remove(removeFrequency);
        }
    }

    /// <summary>
    /// Метод для создания и демонстративного сжигания мирного договора при расторжении такового.
    /// Если сжечь не получается, то просто удаляем сущность.
    /// </summary>
    /// <param name="user"> Пользователь, который нажал кнопку расторжения мирного договора </param>
    /// <param name="prototype"> Прототип листка мирного договора, который будет демонстративно сожжен </param>
    private void TryIgnitePaperWhenTreatyTerminate(EntityUid user, string prototype)
    {
        var coords = Transform(user).Coordinates;
        var item = Spawn(prototype, coords);

        if (TryComp<FlammableComponent>(item, out var flammableComponent))
        {
            flammableComponent.FireStacks = flammableComponent.MaximumFireStacks;
            _flammableSystem.Ignite(item, user, flammableComponent);
        }
        else
        {
            QueueDel(item);
        }
    }
}
