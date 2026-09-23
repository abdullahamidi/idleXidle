# VFX blend correction — renderer experiment

**Date** 2026-09-23 · **Branch** `fix/vfx-fade` (not merged) · **Type** renderer contract + visual validation
**Scope** `VfxPlayer`'s effect pass only. No asset was regenerated or edited; `fx_seeker_strike` is still the
pilot strip from 63193bb (SHA-1 `0cf83738…`). No size, timing, prompt, gate wiring, rarity or Forge change.
**Decision record** `docs/architecture/ADR-009-vfx-premultiplied-additive-blend.md` (Proposed)

## 1–3. The equations, verified in production (not assumed)

Read from the shipped MonoGame 3.8.4.1 assembly with a probe:

| | ColorSrc | ColorDst | AlphaSrc | AlphaDst | Function |
|---|---|---|---|---|---|
| `BlendState.Additive` (old) | **SourceAlpha** | One | SourceAlpha | One | Add |
| `VfxBlend.PremultipliedAdditive` (new) | **One** | One | One | One | Add |

**Loaded VFX data is premultiplied.** `Texture2D.FromStream` returns straight alpha (a texel stored as
200,100,50,128 loads unchanged); `AssetLibrary.Premultiply` then makes it 100,50,25,128. Every VFX
strip goes through it (eager and deferred load paths alike). SpriteBatch multiplies the texel by the draw
colour, and `Color * f` scales all four channels.

    old   out = dst + (texel.rgb·α · tint.rgb) × (α · tint.a)     = dst + rgb·α²·tint.rgb·tint.a
    new   out = dst +  texel.rgb·α · Light(tint).rgb               = dst + rgb·α ·tint.rgb·tint.a
          Light(tint) = tint × tint.a

The tint's opacity had been squared too: that covers the cubed tail fade (really k⁶), the barrier's
0.40–0.60 breathing, the aura's 0.38 → 1 pulse and the break's × 0.8. `VfxBlend.Light` keeps that
response on purpose, so the correction removes exactly one factor: the texel's second α. For α ∈ {0, 1}
the old and new equations are identical at every opacity. A bare One/One swap would have made the
standing barrier 2.5× brighter at rest.

## 4. The Seeker's HARD HANDS, in HUNT (01, 02, 06, 07)

Films are real game frames. `capture_seq.sh` now calls `ResetElapsedTime()` after each saved picture,
so one picture is one 16.7 ms step; before this fix, the saves' catch-up updates made a 0.67 s effect
show on two frames. Each pair is aligned on its own hit frame (seek jitter moves the hit by 3–4 frames
between runs).

- **Readability:** clearly better. From 0 to 133 ms after the blow, AFTER shows a radial burst of
  soft-tipped red shards around the impact; BEFORE showed a few lines. On the killing blow it now reads
  as the Seeker's burst.
- **Soft rays:** they stay soft, and the tips fade. **No rectangle** returns, and there is **no giant bloom**.
- **Central impact:** readable. The centre is also covered by the separate pale `fx_weakhit` puff that
  plays on the same hit (in both columns).
- **Lookup:** `cast.strike key=fx_seeker_strike_strip8_512`, ratio 0.276, at UI 100, 125 and 150 %. The
  `vfxdebug` placement dump is **byte-identical** before and after, so no size or placement moved.

## 5. An old one-bit effect (03, 04)

| Effect (alpha values 0 and 255 only) | Mean luminance of its region, before → after |
|---|---|
| `fx_seeker_trap`, cast in the Seeker film (three frames in) | +0.1 %, +0.1 %, +0.1 % |
| `fx_seeker_trap`, `vfxdebug` pose | −0.7 % |
| `fx_chorus_trap` | +0.2 % |

Visually identical. The only mechanism that can change a one-bit strip is bilinear filtering: an edge
texel sampled at 0.3× blends to partial alpha, and it now adds light once instead of squared. That
shows as nothing measurable here.

## 6–7. Existing soft-alpha effects: did anything get too bright? (04, 05)

The library's partial alpha lives mostly in the HELD field auras, which is the real compatibility risk:

| Strip | Partial share of lit pixels | Mean added light at rest, old → new | Peak texel light |
|---|---|---|---|
| `fx_aura` | 91 % (mean α 83) | 0.0055 → 0.0074 (×1.36) | 1.00 → 1.00 |
| `fx_press` | 89 % (mean α 228) | 0.0148 → 0.0161 (×1.09) | 0.91 → 0.91 |
| `fx_wilt` | 62 % | 0.0101 → 0.0113 (×1.12) | 0.80 → 0.80 |

In game, `fx_press` (behind the hunter in every Seeker fight) measured +0.2 % and +1.1 % with the
barrier, and is visually identical. `fx_aura` cannot be posed by the fight fixture: its field is fixed to
PRESS whoever hunts. It was judged with the verified equations (05). At the pulse peak its soft outer
flame tongues show more, and its hollow middle gains a faint haze. That is visible side by side but
not washed out. **No effect became too bright.** `fx_aura` is the one to watch when a field that wears it
is next looked at in play.

## 8. The Seeker collapse / re-burst — still objectionable

About 300–450 ms after the blow, after the flash and the puff have cleared, a **separate compact red
starburst** appears and expands again. These are strip frames 4–7, mostly before the tail fade starts at
433 ms. The old blend hid it. The correct blend makes it plainly visible, on both the killing blow and a
surviving target.

It does not read as recoil: recoil would continue the burst's motion. This is a gap, then a new small
explosion, and it reads as a second hit. **It is an animation-generation defect.** The pilot strip should be
regenerated (this one strip only) before any batch.

## 9. Gates

- `dotnet build IdleXIdle.sln -warnaserror`: 0 warnings, 0 errors
- Core 1,850 · Game 712 (707 + 5 new) · Integration 2: all passed, 0 failed
- `tools/check_all.sh`: all gates green. `check_fx_edges.py` is still **not** wired in.
- `vfx_blend_contract_test.cs` was mutation-checked: putting `BlendState.Additive` back into `VfxPlayer`
  fails `test_the_vfx_pass_begins_with_the_contract_blend`.

## Files

`01`/`02`: Seeker strike, killing blow and surviving target, BEFORE over AFTER at 2× zoom.
`03`: the trap film. `04`: held effects and two one-bit traps. `05`: field-aura simulation.
`06`/`07` `*_realspeed.gif`: side by side at exact play speed (every 2nd game frame, delays 30/30/40 ms);
`*_slow.gif`: every game frame at about ¼ speed.
