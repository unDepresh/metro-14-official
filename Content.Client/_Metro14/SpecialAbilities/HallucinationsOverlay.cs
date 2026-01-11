using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Metro14.SpecialAbilities;

public sealed class HallucinationsOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Hallucinations";
    private readonly ShaderInstance _hallucinationsShader;

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly Texture _hallucinationsTexture;

    public HallucinationsOverlay()
    {
        IoCManager.InjectDependencies(this);
        _hallucinationsShader = _prototypeManager.Index(Shader).InstanceUnique();
        _hallucinationsTexture = _resourceCache.GetResource<TextureResource>("/Textures/_Metro14/Effects/hallucinations.png").Texture;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;

        _hallucinationsShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _hallucinationsShader.SetParameter("noise_tex", _hallucinationsTexture);
        _hallucinationsShader.SetParameter("percentComplete", 0.5f);
        _hallucinationsShader.SetParameter("burn_size", 0.2f);
        _hallucinationsShader.SetParameter("shit_size", 0.5f);
        _hallucinationsShader.SetParameter("burn_color", new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

        handle.UseShader(_hallucinationsShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}