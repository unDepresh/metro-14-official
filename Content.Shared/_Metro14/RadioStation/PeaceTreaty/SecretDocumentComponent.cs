using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Metro14.RadioStation.PeaceTreaty;

[RegisterComponent]
public sealed partial class SecretDocumentComponent : Component
{
    /// <summary>
    /// Список частот союзников. Допускаю, что на некоторых картах будут союзники раундстартом. Например, сражение союзников КЛ против Гидры.
    /// </summary>
    [DataField]
    public HashSet<string> AlliesFractions = new HashSet<string>();

    /// <summary>
    /// Список частот фракций, которые не могут быть союзниками.
    /// Например, для КЛ такой частотой будет обладать фракция Гидра.
    /// </summary>
    [DataField]
    public List<string> CannotBeAllies = new List<string>();

    /// <summary>
    /// Какой фракции принадлежит документ. Частота документа должна совпадать с одной из частот вышек на карте.
    /// </summary>
    [DataField]
    public string? CurrentFrequency = null;

    /// <summary>
    /// Прототип листка мирного договора.
    /// </summary>
    [DataField]
    public string? FractionPeaceTreatyPrototype = null;

    [DataField]
    public Dictionary<string, string> FrequenciesLocalizationMapping = new()
    {
        { "hydra_frequency", "peace-hydra-frequency-named" },
        { "redline_frequency", "peace-redline-frequency-named" },
        { "hansa_frequency", "peace-hansa-frequency-named" },
        { "sparta_frequency", "peace-sparta-frequency-named" },
        { "tech_frequency", "peace-tech-frequency-named" },
        { "vdnh_frequency", "peace-vdnh-frequency-named" }
    };
}
