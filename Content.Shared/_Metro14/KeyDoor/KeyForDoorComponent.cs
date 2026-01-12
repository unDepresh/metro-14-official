using Robust.Shared.Serialization;

namespace Content.Shared._Metro14.KeyDoor;

[RegisterComponent]
public sealed partial class KeyForDoorComponent : Component
{
    /// <summary>
    /// Список доступов, которые предоставляет данный ключ.
    /// </summary>
    [DataField]
    public List<string> AccessList = new List<string>();
}
