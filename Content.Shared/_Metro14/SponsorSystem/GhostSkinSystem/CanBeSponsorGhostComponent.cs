using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System.Collections.Generic;

namespace Content.Server._Metro14.SponsorSystem.GhostSkinSystem;

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
    [DataField]
    public Dictionary<string, string> SponsorStates = new()
    {
        { "soldier", "ghost_camo" },
        { "lieutenant", "ghost_fire" },
        { "colonel", "ghost_blazeit" },
        { "beatus_individual_tier", "ghostburger" },
        { "ramzesina_individual_tier", "god" },
        { "kompotik_individual_tier", "uncloak" }
    };
}

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
