using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// BRAND's curse shader (ADR-013 §1, §7): the committed <c>assets/shaders/brand_curse.mgfxo</c> (compiled from
/// <c>Content/Shaders/BrandCurse.fx</c> by <c>tools/shaders/build_shaders.sh</c>), loaded once with
/// <c>new Effect(device, bytes)</c>, and the packed territory atlas (<see cref="CurseAtlas"/>) it samples, uploaded once.
/// Both are disposed with the device.
/// </summary>
/// <remarks>
/// <para>
/// USE: per afflicted creature, set its host (<see cref="SetHost"/>), its frame (<see cref="SetFrame"/>), its territories
/// (<see cref="SetTerritory"/>, <see cref="ClearTerritories"/>) and puffs, <see cref="Commit"/>, then draw the creature's
/// current frame once more inside <c>SpriteBatch.Begin(..., BlendState.AlphaBlend, ..., effect: </c><see cref="Effect"/><c>)</c>
/// with the creature's own tint. Parameters change only between draws (the arena batch is broken for each afflicted
/// creature anyway), never inside one.
/// </para>
/// <para>
/// Every parameter handle is cached at load: a frame does no string lookups and allocates nothing (the arrays are
/// reused and copied into the effect on <see cref="Commit"/>).
/// </para>
/// </remarks>
internal sealed class BrandCurseEffect : IDisposable
{
    /// <summary>The compiled shader beside the executable.</summary>
    public const string RelativePath = "assets/shaders/brand_curse.mgfxo";

    private readonly GraphicsDevice _device;
    private readonly EffectParameter _hostLevels, _drainTint, _bruise, _hostEmission, _shade, _uvToPx;
    private readonly EffectParameter _maskU, _maskV, _grow, _stain, _light, _add, _puffAt, _puffColour;
    private readonly Vector4[] _maskUv = new Vector4[CurseMaterial.ShaderTerritories], _maskVv = new Vector4[CurseMaterial.ShaderTerritories],
                               _growV = new Vector4[CurseMaterial.ShaderTerritories], _stainV = new Vector4[CurseMaterial.ShaderTerritories],
                               _lightV = new Vector4[CurseMaterial.ShaderTerritories], _addV = new Vector4[CurseMaterial.ShaderTerritories],
                               _puffAtV = new Vector4[CurseMaterial.MaxPuffs], _puffColourV = new Vector4[CurseMaterial.MaxPuffs];
    private bool _disposed;

    /// <summary>The curse's effect: pass it to <c>SpriteBatch.Begin</c>.</summary>
    public Effect Effect { get; }

    /// <summary>The packed territory atlas (its puff cell, <see cref="CurseAtlas.PuffSource"/>, draws the wisps).</summary>
    public Texture2D Atlas { get; }

    private BrandCurseEffect(GraphicsDevice device, byte[] bytecode)
    {
        _device = device;
        Effect = new Effect(device, bytecode);
        Atlas = CurseAtlas.Approved.Upload(device);
        var p = Effect.Parameters;
        Required(p, "CurseAtlas").SetValue(Atlas);
        _hostLevels = Required(p, "HostLevels");
        _drainTint = Required(p, "DrainTint");
        _bruise = Required(p, "Bruise");
        _hostEmission = Required(p, "HostEmission");
        _shade = Required(p, "Shade");
        _uvToPx = Required(p, "UvToPx");
        _maskU = Required(p, "MaskU");
        _maskV = Required(p, "MaskV");
        _grow = Required(p, "Grow");
        _stain = Required(p, "Stain");
        _light = Required(p, "Light");
        _add = Required(p, "Add");
        _puffAt = Required(p, "PuffAt");
        _puffColour = Required(p, "PuffColour");
        ClearTerritories();
        ClearPuffs();
        Commit();
        device.Disposing += OnDeviceDisposing;
    }

    /// <summary>Loads the compiled shader (from <paramref name="path"/>, by default beside the executable), uploads the
    /// atlas and warms the shader (<see cref="Warm"/>). Load time only.</summary>
    public static BrandCurseEffect Load(GraphicsDevice device, string? path = null)
    {
        var fx = new BrandCurseEffect(device, File.ReadAllBytes(path ?? Path.Combine(AppContext.BaseDirectory, RelativePath)));
        fx.Warm();
        return fx;
    }

    /// <summary>
    /// Draws one empty curse pass into a throwaway 4x4 target, at load: the driver links the shader program on its first
    /// draw (measured: ~9-12 ms and ~46 KB of managed allocation on the frame the first creature was cursed), so that
    /// cost is paid here, never on a combat frame. The render targets and scissor set before are restored.
    /// </summary>
    private void Warm()
    {
        var previous = _device.GetRenderTargets();
        var scissorBefore = _device.ScissorRectangle;
        using var target = new RenderTarget2D(_device, 4, 4);
        using var batch = new SpriteBatch(_device);
        using var scissor = new RasterizerState { ScissorTestEnable = true };   // (the arena's state: the same first-use path)
        _device.SetRenderTarget(target);
        _device.ScissorRectangle = new Rectangle(0, 0, 4, 4);
        SetHost(default);
        SetFrame(Vector4.One);
        ClearTerritories();
        ClearPuffs();
        Commit();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, scissor, Effect);
        batch.Draw(Atlas, new Rectangle(0, 0, 4, 4), CurseAtlas.PuffSource, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 0f);
        batch.End();
        _device.SetRenderTargets(previous);
        _device.ScissorRectangle = scissorBefore;
    }

    /// <summary>The creature's host constants.</summary>
    public void SetHost(in CurseMaterial.HostParams h)
    {
        _hostLevels.SetValue(new Vector4(h.Luma, h.DrainedMean, h.AshMean, 0f));
        _drainTint.SetValue(h.DrainTint);
        _bruise.SetValue(h.Bruise);
        _hostEmission.SetValue(h.Emission);
        _shade.SetValue(h.Shade);
    }

    /// <summary>The drawn frame's host-UV to screen-pixel affine (<see cref="CurseMaterial.UvToPx"/>; the puffs use it).</summary>
    public void SetFrame(Vector4 uvToPx) => _uvToPx.SetValue(uvToPx);

    /// <summary>Territory <paramref name="k"/>'s parameters (taken on <see cref="Commit"/>).</summary>
    public void SetTerritory(int k, in CurseMaterial.TerritoryParams t)
    {
        _maskUv[k] = t.MaskU;
        _maskVv[k] = t.MaskV;
        _growV[k] = new Vector4(t.At, t.DrainAlpha, t.BurnAlpha, t.InvSwell);
        _stainV[k] = new Vector4(t.Tissue, t.Pale, t.Vein, t.Edge);
        _lightV[k] = new Vector4(t.Glow, t.Emit, t.Burn, t.Accent);
        _addV[k] = new Vector4(t.Fissure, t.Front, t.EmitAdd, t.AccentGroup);
    }

    /// <summary>Every territory draws nothing.</summary>
    public void ClearTerritories()
    {
        for (var k = 0; k < CurseMaterial.ShaderTerritories; k++) SetTerritory(k, new CurseMaterial.TerritoryParams { InvSwell = 1f });
    }

    /// <summary>Puff <paramref name="i"/> (taken on <see cref="Commit"/>).</summary>
    public void SetPuff(int i, in CurseMaterial.PuffParams puff)
    {
        _puffAtV[i] = new Vector4(puff.Centre, puff.Radius > 0f ? 1f / puff.Radius : 0f, 0f);
        _puffColourV[i] = puff.Radius > 0f ? new Vector4(puff.Colour, 0f) : Vector4.Zero;
    }

    /// <summary>No puff draws.</summary>
    public void ClearPuffs()
    {
        for (var i = 0; i < CurseMaterial.MaxPuffs; i++)
        {
            _puffAtV[i] = Vector4.Zero;
            _puffColourV[i] = Vector4.Zero;
        }
    }

    /// <summary>Copies the territories and puffs into the effect (before the creature's draw).</summary>
    public void Commit()
    {
        _maskU.SetValue(_maskUv);
        _maskV.SetValue(_maskVv);
        _grow.SetValue(_growV);
        _stain.SetValue(_stainV);
        _light.SetValue(_lightV);
        _add.SetValue(_addV);
        _puffAt.SetValue(_puffAtV);
        _puffColour.SetValue(_puffColourV);
    }

    /// <summary>Is this the live effect for <paramref name="device"/> (not disposed with an earlier device)?</summary>
    public bool IsFor(GraphicsDevice device) => !_disposed && ReferenceEquals(_device, device);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _device.Disposing -= OnDeviceDisposing;
        Effect.Dispose();
        Atlas.Dispose();
    }

    private void OnDeviceDisposing(object? sender, EventArgs e) => Dispose();

    private static EffectParameter Required(EffectParameterCollection p, string name)
        => p[name] ?? throw new InvalidDataException($"{RelativePath}: no parameter '{name}' (rebuild with tools/shaders/build_shaders.sh)");
}
