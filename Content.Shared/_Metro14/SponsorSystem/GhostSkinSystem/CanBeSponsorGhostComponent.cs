using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System.Collections.Generic;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Metro14.SponsorSystem.GhostSkinSystem;

[RegisterComponent]
public sealed partial class CanBeSponsorGhostComponent : Component
{
    /// <summary>
    /// Состояние спрайта по умолчанию для не-спонсоров  
    /// </summary>
    [DataField]
    public string DefaultState = "ghost";

    /// <summary>
    /// Маппинг уровней спонсора на состояния спрайта  
    /// </summary>
    public Dictionary<string, List<string>> SponsorStates = new()
    {
        ["ghost"] = new() { "ghost" },
        ["soldier"] = new() { "ghost", "ghost_camo" },
        ["lieutenant"] = new() { "ghost", "ghost_camo", "ghost_fire" },
        ["colonel"] = new() { "ghost", "ghost_camo", "ghost_fire", "ghost_blazeit" },
        ["beatus_individual_tier"] = new() { "ghost", "ghost_camo", "ghost_fire", "ghost_blazeit", "ghostburger" },
        ["ramzesina_individual_tier"] = new() { "ghost", "ghost_camo", "ghost_fire", "ghost_blazeit", "god" },
        ["kompotik_individual_tier"] = new() { "ghost", "ghost_camo", "ghost_fire", "ghost_blazeit", "ghost_kompotik" }
    };

    public List<string> PossibleSkins = new List<string>();

    [DataField]
    public int CurrentIndex = 0;

    /// <summary>
    /// Сущность, хранящая действие смены скина наблюдателя.
    /// </summary>
    [DataField]
    public EntityUid? TryChangeGhostSkinActionEntity;

    /// <summary>
    /// ID прототипа действия смены скина наблюдателя.
    /// </summary>
    [DataField]
    public EntProtoId TryChangeGhostSkinAction = "ActionChangeGhostSkin";
}

/// <summary>
/// Событие, поднимаемое при нажатии кнопки-действия для переключения спрайта.
/// </summary>
public sealed partial class TryChangeGhostSkinActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public enum GhostVisuals : byte
{
    SpriteState,
}

[Serializable, NetSerializable]
public enum GhostVisualLayers : byte
{
    Base,
}
