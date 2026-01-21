using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Metro14.RadioStation.PeaceTreaty;

[RegisterComponent]
public sealed partial class PeaceTreatyComponent : Component
{
    /// <summary>
    /// Частота фракции, что предложили мирное соглашение.
    /// </summary>
    [DataField]
    public string? Frequency = null;
}

[Serializable, NetSerializable]
public sealed partial class TerminatePeaceTreatyEvent : EntityEventArgs
{
    /// <summary>
    /// Частота секретного документа, у которого выбрали действие.
    /// </summary>
    public string? SecretDocumentFrequency = null;

    /// <summary>
    /// Выбранная игроком частота для расторжения мирного договора.
    /// </summary>
    public string PeaceTreatyFrequency;

    /// <summary>
    /// Uid пользователя, который нажал кнопку.
    /// </summary>
    public NetEntity User;

    public TerminatePeaceTreatyEvent(string? secretDocumentFrequency, string peaceTreatyFrequency, NetEntity user)
    {
        SecretDocumentFrequency = secretDocumentFrequency;
        PeaceTreatyFrequency = peaceTreatyFrequency;
        User = user;
    }
}

[Serializable, NetSerializable]
public sealed partial class SpawnPeaceTreatyEvent : EntityEventArgs
{
    /// <summary>
    /// Uid пользователя, который нажал кнопку.
    /// </summary>
    public NetEntity User;

    /// <summary>
    /// Прототип, который нужно заспавнить при извлечении из секретного документа мирного договора.
    /// </summary>
    public string? Prototype = null;

    public SpawnPeaceTreatyEvent(NetEntity user, string? prototype)
    {
        User = user;
        Prototype = prototype;
    }
}
