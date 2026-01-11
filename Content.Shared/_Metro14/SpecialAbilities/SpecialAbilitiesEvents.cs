using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Robust.Shared.Network;

namespace Content.Shared._Metro14.SpecialAbilities;

public sealed partial class SpecialSleepActionEvent : InstantActionEvent { }
public sealed partial class SpecialKnockbackActionEvent : InstantActionEvent { }
public sealed partial class SpecialShaderActionEvent : InstantActionEvent { }
public sealed partial class SpecialInvisibilityActionEvent : InstantActionEvent
{
    public bool ToggledOn { get; set; }
}

[Serializable, NetSerializable]
public sealed class ApplyHallucinationShaderEvent : EntityEventArgs
{
    public NetEntity Target { get; }
    public TimeSpan Duration { get; }

    public ApplyHallucinationShaderEvent(NetEntity target, TimeSpan duration)
    {
        Target = target;
        Duration = duration;
    }
}