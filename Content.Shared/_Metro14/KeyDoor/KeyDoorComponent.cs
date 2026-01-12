using Robust.Shared.Serialization;

namespace Content.Shared._Metro14.KeyDoor;

[RegisterComponent]
public sealed partial class KeyDoorComponent : Component
{
    /// <summary>
    /// Список необходимых доступов для открытия двери.
    /// </summary>
    [DataField]
    public List<string> Access = new List<string>();

    /// <summary>
    /// Спрайт для двери в открытом состоянии.
    /// </summary>
    [DataField("openSpriteState", required: true)]
    public string OpenSpriteState = "open";

    /// <summary>
    /// Спрайт для двери в закрытом состоянии.
    /// </summary>
    [DataField("closeSpriteState", required: true)]
    public string CloseSpriteState = "closed";

    [DataField]
    public string OpenSound = "/Audio/Effects/stonedoor_openclose.ogg";

    [DataField]
    public string CloseSound = "/Audio/Effects/stonedoor_openclose.ogg";

    [DataField]
    public bool IsOpen = false;
}

[Serializable, NetSerializable]
public enum KeyDoorVisuals : byte
{
    SpriteState,
}

[Serializable, NetSerializable]
public enum KeyDoorVisualLayers : byte
{
    Base,
}

