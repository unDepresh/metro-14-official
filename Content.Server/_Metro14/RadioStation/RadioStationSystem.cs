using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server._Metro14.GameRules;
using Content.Server.RoundEnd;
using Content.Shared.GameTicking;
using Content.Shared._Metro14.RadioStation;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Metro14.RadioStation;

/// <summary>
/// Система управления радиостанциями. Отслеживает состояние вышек,
/// управляет частотами фракций и обрабатывает их поражение.
/// </summary>
public sealed class RadioStationSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RadioStationComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RadioStationComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RadioStationComponent, RadioStationChangeFrequencieDoAfterEvent>(OnChangeFrequencie);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    /// <summary>
    /// Время, в течение которого возможно возродить фракцию 
    /// после потери последней вышки (в секундах).
    /// Данное значение перезаписывается при инициализации карты, то есть доступен вариант
    /// создания радиостанций под быстрый режим игры.
    /// </summary>
    private int _timeToDefeat = 900;

    /// <summary>
    /// Сопоставление частот и ролей на начало раунда.
    /// Ключ: частота, Значение: список прототипов ролей
    /// </summary>
    private Dictionary<string, List<string>> _roundstartFrequencies = new Dictionary<string, List<string>>();

    /// <summary>
    /// Текущие активные частоты на карте (обновляется при проверках)
    /// </summary>
    private HashSet<string> _currentFractionFrequencies = new HashSet<string>();

    /// <summary>
    /// Заблокированные частоты проигравших фракций
    /// </summary>
    private List<string> _blockedFractionFrequencies = new List<string>();

    /// <summary>
    /// Фракции, недавно потерявшие последнюю вышку.
    /// Ключ: частота, Значение: время удаления из словаря
    /// </summary>
    private Dictionary<string, TimeSpan> _defeatedFractions = new Dictionary<string, TimeSpan>();

    /// <summary>
    /// Локализованные названия частот для отображения в UI
    /// </summary>
    public static Dictionary<string, string> _frequenciesLocalizationMapping = new Dictionary<string, string>();

    /// <summary>
    /// Поле для отслеживания состояния раунда.
    /// Значении true - отсчет окончания раунда идет.
    /// </summary>
    private bool CountdownIsOn = false;

    /// <summary>
    /// Время окончания раунда.
    /// </summary>
    private TimeSpan FinalTime = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Отслеживает статус проигравших фракций и блокирует их по истечении времени.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var defeatedFraction in _defeatedFractions)
        {
            if (_gameTiming.CurTime < defeatedFraction.Value)
                continue;

            _blockedFractionFrequencies.Add(defeatedFraction.Key);
            _defeatedFractions.Remove(defeatedFraction.Key);
        }

        if (CountdownIsOn)
        {
            if (_gameTiming.CurTime >= FinalTime)
            {
                _roundEndSystem.EndRound();
            }
        }
    }

    /// <summary>
    /// При перезапуске раунда очищаем все, чтобы не было проблем с картами, которые содержат иные радиостанции.
    /// </summary>
    /// <param name="ev"></param>
    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        CountdownIsOn = false;

        FinalTime = TimeSpan.FromSeconds(60);
        _timeToDefeat = 900;

        _roundstartFrequencies.Clear();
        _currentFractionFrequencies.Clear();
        _blockedFractionFrequencies.Clear();
        _defeatedFractions.Clear();
        _frequenciesLocalizationMapping.Clear();
    }

    /// <summary>
    /// При инициализации компонента первой вышки на карте составляется список доступных частот.
    /// </summary>
    /// <param name="uid"> Радиостанция </param>
    /// <param name="component"> Компонент радиостанции </param>
    /// <param name="args"> Аргументы события инициализации карты </param>
    private void OnMapInit(EntityUid uid, RadioStationComponent component, MapInitEvent args)
    {
        if (_roundstartFrequencies.Count == 0 && component.RolesFrequenciesMapping.Count != 0)
        {
            _frequenciesLocalizationMapping = new Dictionary<string, string>(component.FrequenciesLocalizationMapping);
            _roundstartFrequencies = new Dictionary<string, List<string>>(component.RolesFrequenciesMapping);
            _timeToDefeat = component.TimeToRecaptureStation;
        }
    }

    /// <summary>
    /// При осмотре радиостанции необходимо явно показывать установленную частоту для облегчения геймлпея.
    /// </summary>
    /// <param name="uid"> Радиостанция </param>
    /// <param name="component"> Компонент радиостанции </param>
    /// <param name="args"> Аргументы события осмотра сущности </param>
    private void OnExamine(EntityUid uid, RadioStationComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (component.FrequenciesLocalizationMapping.ContainsKey(component.CurrentFrequence))
            args.PushMarkup(Loc.GetString($"radistation-frequency-description", ("frequency", Loc.GetString(component.FrequenciesLocalizationMapping[component.CurrentFrequence]))));
    }

    /// <summary>
    /// Обработчик события, которое поднимается при попытке сменить частоту.
    /// </summary>
    /// <param name="uid"> Радиостанция </param>
    /// <param name="component"> Компонент радиостанции </param>
    /// <param name="args">
    /// Аргументы события попытки сменить частоту точки связи.
    /// Наследуется от SimpleDoAfterEvent, кастомных аргументов не содержит
    /// </param>
    private void OnChangeFrequencie(EntityUid uid, RadioStationComponent component, RadioStationChangeFrequencieDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (_mindSystem.TryGetMind(args.User, out var mindId, out var mind))
        {
            foreach (var role in mind.MindRoleContainer.ContainedEntities)
            {
                if (TryComp<MindRoleComponent>(role, out var roleComp))
                {
                    string roleId = "";

                    if (roleComp.JobPrototype is not null)
                        roleId = roleComp.JobPrototype.Value;
                    else if (roleComp.AntagPrototype is not null)
                        roleId = roleComp.AntagPrototype.Value;

                    bool flag = false;
                    foreach (var rolesFrequencies in _roundstartFrequencies)
                    {
                        if (rolesFrequencies.Value.Contains(roleId) && !_blockedFractionFrequencies.Contains(rolesFrequencies.Key))
                        {
                            component.CurrentFrequence = rolesFrequencies.Key;
                            TryFindDefeatedFractions();

                            if (_defeatedFractions.ContainsKey(component.CurrentFrequence))
                                _defeatedFractions.Remove(component.CurrentFrequence);

                            _popupSystem.PopupEntity(Loc.GetString("radistation-reconfigured-successfully"), uid);
                            flag = false;
                            break;
                        }
                        else
                        {
                            flag = true;
                        }
                    }

                    if (flag)
                        _popupSystem.PopupEntity(Loc.GetString("radistation-reconfigured-failed"), uid);
                }
            }
        }

        args.Handled = true;
    }

    /// <summary>
    /// Метод, который сравнивает изначальный список частот и текущий для обнаружения "проигравших" фракций.
    /// Если же частоты фракции еще нет в списке на убывание или в списке заблокированных частот, то
    /// она добавляется туда.
    /// </summary>
    private void TryFindDefeatedFractions()
    {
        _currentFractionFrequencies.Clear();

        var query = EntityManager.AllEntityQueryEnumerator<RadioStationComponent>();
        while (query.MoveNext(out var uid, out var radioStationComponent))
        {
            if (string.Compare(radioStationComponent.CurrentFrequence, "neutral_frequency") == 0)
                continue;

            _currentFractionFrequencies.Add(radioStationComponent.CurrentFrequence);
        }

        if (RadioStationRuleSystem.IsEnabledRule)
        {
            if (_currentFractionFrequencies.Count == 1)
            {
                CountdownIsOn = true;
                FinalTime = _gameTiming.CurTime + TimeSpan.FromSeconds(_timeToDefeat);
            }
            else if (CountdownIsOn)
            {
                CountdownIsOn = false;
            }
        }

        List<string> missingFrequencies = _roundstartFrequencies.Keys.Except(_currentFractionFrequencies).ToList();

        foreach(string frequence in missingFrequencies)
        {
            if (_defeatedFractions.ContainsKey(frequence) || _blockedFractionFrequencies.Contains(frequence))
                continue;

            _defeatedFractions.Add(frequence, _gameTiming.CurTime + TimeSpan.FromSeconds(_timeToDefeat));

            HashSet<ICommonSession> temoHashSet = new HashSet<ICommonSession>();
            foreach (string jobId in _roundstartFrequencies[frequence])
            {
                temoHashSet.UnionWith(GetPlayersByRole(jobId).ToList());
            }

            SendMessageToRole(temoHashSet, Loc.GetString("last-radio-station-lost-message", ("time", _timeToDefeat / 60)));
        }
    }

    /// <summary>
    /// Вспомогательный метод для отправки сообщения всем участникам фракции о захвате их последней радиостанции.
    /// </summary>
    /// <param name="roleId"> Критерий, по которому находятся игроки для отправки им информационного сообщения </param>
    /// <param name="message"> Сообщение, которое будет показано участникам фракции, потерявшей последнюю точку связи, чьи роли соответствуют критерию. </param>
    public void SendMessageToRole(HashSet<ICommonSession> findPlayres, string message)
    {
        var targetPlayers = findPlayres.ToList();
        var clients = targetPlayers.Select(p => p.Channel);

        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));

        _chatManager.ChatMessageToMany(
            ChatChannel.Server,
            message,
            wrappedMessage,
            default,
            false,
            true,
            clients);
    }

    /// <summary>
    /// Вспомогательный метод для поиска игроков с соответсвующей ролью.
    /// Необходимо для оповещения о захвате всех точек связи фракции.
    /// </summary>
    /// <param name="roleId"> Критерий поиска игроков </param>
    /// <returns> Сессии игроков, чьи роли соответствуют критериям </returns>
    public IEnumerable<ICommonSession> GetPlayersByRole(string roleId)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (_mindSystem.TryGetMind(session, out var mindId, out var mindComp))
            {
                // Проверка роли работы (job)  
                if (_jobs.MindHasJobWithId(mindId, roleId))
                {
                    yield return session;
                }

                // Проверка роли сознания по прототипу  
                var allRoles = _role.MindGetAllRoleInfo(mindId);
                if (allRoles.Any(role => role.Prototype == roleId))
                {
                    yield return session;
                }
            }
        }
    }
}
