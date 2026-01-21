using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server._Metro14.GameRules.Components;
using Content.Server._Metro14.RadioStation;
using Content.Server._Metro14.RadioStation.PeaceTreaty;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared._Metro14.RadioStation;

namespace Content.Server._Metro14.GameRules;

/// <summary>
/// Система управления режима захвата точек.
/// </summary>
public sealed class RadioStationRuleSystem : GameRuleSystem<RadioStationRuleComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    /// <summary>
    /// Поле отражающее, выбран ли в данный момент режим захвата радиостанций.
    /// Это необходимо для того, чтобы при захвате последней радиостанции раунд закончился.
    /// </summary>
    public static bool IsEnabledRule = false;

    /// <summary>
    /// Информация о суммарном количестве точек на карте.
    /// </summary>
    private int _radiostationSummaryCount = 0;

    /// <summary>
    /// Информация о количестве захваченных точек.
    /// </summary>
    private int _radiostationCapturedCount = 0;

    /// <summary>
    /// Частота фракции-лидера.
    /// </summary>
    private string _radiostationLeaderFrequency = "";

    /// <summary>
    /// Частота фракции-лидера без локализации.
    /// </summary>
    private string _radiostationLeaderFrequencyWithoutLocalization = "";

    /// <summary>
    /// Базовый метод инициализации правила в игре.
    /// </summary>
    protected override void Added(EntityUid uid, RadioStationRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        IsEnabledRule = true;
    }

    /// <summary>
    /// На всякий случай при перезапуске раунда очищаем всю информацию.
    /// </summary>
    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        IsEnabledRule = false;

        _radiostationSummaryCount = 0;
        _radiostationCapturedCount = 0;
        _radiostationLeaderFrequency = "";
        _radiostationLeaderFrequencyWithoutLocalization = "";
    }

    /// <summary>
    /// Обработчик окончания раунда.
    /// </summary>
    protected override void AppendRoundEndText(EntityUid uid, RadioStationRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        SetFinalInformation();
        args.AddLine(Loc.GetString("radiostation-summary-count", ("count", _radiostationSummaryCount)));
        args.AddLine(Loc.GetString("radiostation-captured-count", ("count", _radiostationCapturedCount)));
        args.AddLine(Loc.GetString("radiostation-leader-frequency", ("frequency", _radiostationLeaderFrequency)));

        if (PeaceTreatySystem.AlliesFractions.Count != 0)
        {
            if (PeaceTreatySystem.FrequenciesLocalizationMapping.ContainsKey(_radiostationLeaderFrequencyWithoutLocalization))
            {
                args.AddLine(Loc.GetString("radiostation-allies-info", ("frequency", Loc.GetString(PeaceTreatySystem.FrequenciesLocalizationMapping[_radiostationLeaderFrequencyWithoutLocalization]))));

                foreach (string frequence in PeaceTreatySystem.AlliesFractions[_radiostationLeaderFrequencyWithoutLocalization])
                {
                    if (PeaceTreatySystem.FrequenciesLocalizationMapping.ContainsKey(frequence))
                        args.AddLine(Loc.GetString("radiostation-allies-info-dop", ("frequency", Loc.GetString(PeaceTreatySystem.FrequenciesLocalizationMapping[frequence]))));
                    else
                        args.AddLine(Loc.GetString("radiostation-allies-info-dop", ("frequency", Loc.GetString(frequence))));
                }
            }
        }
    }

    /// <summary>
    /// Вспомогательный метод, который устанавливает нужную информацию,
    /// чтобы в конце раунда была выведена информации со статистикой.
    /// </summary>
    private void SetFinalInformation()
    {
        int totalStations = 0;
        int capturedStations = 0;
        string? leaderFrequency = null;

        var query = EntityManager.AllEntityQueryEnumerator<RadioStationComponent>();

        Dictionary<string, int> fractionsCount = new Dictionary<string, int>();
        while (query.MoveNext(out var uid, out var radioStationComponent))
        {
            totalStations++;

            bool isNeutral = string.Compare(radioStationComponent.CurrentFrequence, "neutral_frequency") == 0;

            if (!isNeutral)
            {
                capturedStations++;

                if (fractionsCount.ContainsKey(radioStationComponent.CurrentFrequence))
                {
                    fractionsCount[radioStationComponent.CurrentFrequence]++;
                }
                else
                {
                    fractionsCount.Add(radioStationComponent.CurrentFrequence, 1);
                }
            }
        }

        var fractionWinner = fractionsCount.OrderByDescending(kvp => kvp.Value).First();
        if (Content.Server._Metro14.RadioStation.RadioStationSystem._frequenciesLocalizationMapping.TryGetValue(
            fractionWinner.Key,
            out var localizedFreq))
        {
            leaderFrequency = localizedFreq;
            _radiostationLeaderFrequencyWithoutLocalization = fractionWinner.Key;
        }

        _radiostationSummaryCount = totalStations;
        _radiostationCapturedCount = capturedStations;

        if (leaderFrequency != null)
        {
            _radiostationLeaderFrequency = Loc.GetString(leaderFrequency);
        }
    }
}
