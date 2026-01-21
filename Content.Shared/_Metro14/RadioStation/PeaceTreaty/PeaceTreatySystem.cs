using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Metro14.RadioStation.PeaceTreaty;

/// <summary>
/// Система для обработки взаимодействия игрока с секретными документами необходимыми для управления мирными договорами.
/// </summary>
public sealed class PeaceTreatySystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<SecretDocumentComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    /// <summary>
    /// При взаимодействии с секретными документами предлагаем два новых действия: расторгнуть и заключить мирный договор.
    /// </summary>
    private void OnGetInteractionVerbs(EntityUid uid, SecretDocumentComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var category = new VerbCategory(Loc.GetString("peace-treaty-terminate-action"), "/Textures/Interface/VerbIcons/zap.svg.192dpi.png");

        foreach (var frequency in component.AlliesFractions)
        {

            InteractionVerb verb = new()
            {
                Act = () => RaiseLocalEvent(new TerminatePeaceTreatyEvent(component.CurrentFrequency, frequency, GetNetEntity(args.User))),
                Text = Loc.GetString(component.FrequenciesLocalizationMapping[frequency]),
                Category = category,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            };
            args.Verbs.Add(verb);
        }


        InteractionVerb verb2 = new()
        {
            Act = () => RaiseLocalEvent(new SpawnPeaceTreatyEvent(GetNetEntity(args.User), component.FractionPeaceTreatyPrototype)),
            Text = Loc.GetString("peace-treaty-spawn-action"),
            Disabled = false,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
        };

        args.Verbs.Add(verb2);
    }
}
