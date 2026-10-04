using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Vfx;

/// <summary>
/// One composite projectile in flight, and the residue it leaves after it lands (ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// <c>VfxPlayer</c> still owns the flight: its placement, its travel clock and its lifetime are the ones every
/// travelling effect has always had, so a cast's timing and the fight's pacing do not change. This class only
/// replaces the PICTURE: instead of one strip frame, it draws, dimmest first, a soft wake, a short hot core
/// trail, a few shed sparks, the head, and one glint crossing it. When the flight ends it throws a
/// directional impact and lets the wake fade on its own.
/// </para>
/// <para>
/// Bounded by construction: the trail history, the sparks and the shards are fixed arrays sized from the
/// look, and nothing is allocated after launch. A flight submits at most ~40 sprites, in ≤ 6 texture runs.
/// </para>
/// </remarks>
public sealed class ProjectileVisual
{
    private const int HistoryCapacity = 32;

    /// <summary>The composite's recipe (a <see cref="Reset"/> may give the same instance another one).</summary>
    public ProjectileLook Look { get; private set; }

    private int _seed;
    private readonly TrailHistory _trail = new(HistoryCapacity);
    private readonly Particle[] _sparks;
    private readonly Particle[] _shards;
    private int _sparksShed;

    private Texture2D? _head, _trailTex, _glint, _spark, _shard, _flash;
    // DRIVEN (ADR-011): a performance moves the head along its own path and says when it lands; the head is a
    // material sprite (drawn by DrawMaterial, untinted) with an emissive edge (_head) drawn as light.
    private Texture2D? _material;
    private bool _driven;
    private Vector2 _headOrigin;
    private Vector2 _from, _to, _dir, _pos;
    private float _headLen, _headThick, _headScale;
    private float _time, _life, _arrivedAt = -1f;
    private float _contactLife = 1f, _lifeRate;
    private Color _tint;

    private struct Particle
    {
        public Vector2 Position, Velocity;
        public float Born, Life;
        public bool Live;
    }

    /// <summary>A composite for one flight; <paramref name="seed"/> makes its sparks and shards repeatable.</summary>
    public ProjectileVisual(ProjectileLook look, int seed)
    {
        Look = look ?? throw new ArgumentNullException(nameof(look));
        _seed = seed;
        _sparks = new Particle[Math.Max(0, look.Sparks)];
        _shards = new Particle[Math.Max(0, look.ImpactShards)];
    }

    /// <summary>
    /// A REUSABLE composite (the basic attack's missiles, P1.4): its sparks and shards are sized for the most any look it
    /// will be <see cref="Reset"/> to sheds, so one instance flies flight after flight without allocating.
    /// </summary>
    public ProjectileVisual(ProjectileLook look, int seed, int sparkCapacity, int shardCapacity)
    {
        Look = look ?? throw new ArgumentNullException(nameof(look));
        _seed = seed;
        _sparks = new Particle[Math.Max(Math.Max(0, look.Sparks), sparkCapacity)];
        _shards = new Particle[Math.Max(Math.Max(0, look.ImpactShards), shardCapacity)];
    }

    /// <summary>
    /// Re-initialise this instance for a NEW flight of <paramref name="look"/> (nothing allocated): unplaced, unlanded, no
    /// trail, no particles. The look's sparks and shards must fit the capacity this instance was built with.
    /// </summary>
    public void Reset(ProjectileLook look, int seed)
    {
        Look = look ?? throw new ArgumentNullException(nameof(look));
        if (look.Sparks > _sparks.Length || look.ImpactShards > _shards.Length)
            throw new ArgumentException("the look sheds more particles than this composite holds", nameof(look));
        _seed = seed;
        _trail.Clear();
        for (var i = 0; i < _sparks.Length; i++) _sparks[i].Live = false;
        for (var i = 0; i < _shards.Length; i++) _shards[i].Live = false;
        _sparksShed = 0;
        _head = _trailTex = _glint = _spark = _shard = _flash = _material = null;
        _driven = false;
        _headOrigin = Vector2.Zero;
        _from = _to = _pos = Vector2.Zero;
        _dir = Vector2.UnitX;
        _headLen = _headThick = 0f;
        _headScale = 1f;
        _time = _life = _lifeRate = 0f;
        _arrivedAt = -1f;
        _contactLife = 1f;
        _tint = Color.White;
        LastSprites = 0;
        Placed = false;
    }

    // the sparks / shards THIS look sheds (an instance built for reuse may hold more)
    private int SparkCount => Math.Min(_sparks.Length, Math.Max(0, Look.Sparks));
    private int ShardCount => Math.Min(_shards.Length, Math.Max(0, Look.ImpactShards));

    /// <summary>True once the flight has been placed in the arena.</summary>
    public bool Placed { get; private set; }

    /// <summary>True once the head has landed.</summary>
    public bool Landed => _arrivedAt >= 0f;

    /// <summary>The head's drawn length in canvas pixels: the body-size contract's output.</summary>
    public float HeadLengthPx => _headLen;

    /// <summary>Where the head is now.</summary>
    public Vector2 Position => _pos;

    /// <summary>How many trail samples are held (bounded by the history's capacity).</summary>
    public int TrailSamples => _trail.Count;

    /// <summary>The <paramref name="k"/>-th newest trail sample (0 = the newest): where the rear really was.</summary>
    public TrailSample Trail(int k) => _trail[k];

    /// <summary>How many sparks this flight has shed so far (never more than the look's count).</summary>
    public int SparksShed => _sparksShed;

    /// <summary>How many sprites the last <see cref="Draw"/> submitted.</summary>
    public int LastSprites { get; private set; }

    /// <summary>True when the head has landed and everything it left has faded.</summary>
    public bool Finished
    {
        get
        {
            if (!Landed) return false;
            var tail = MathF.Max(MathF.Max(Look.LandedTrailSeconds, Look.SparkSeconds),
                                 MathF.Max(MathF.Max(Look.ImpactSeconds, Look.FlashSeconds), Look.ImpactSlash > 0f ? Look.ImpactSlashSeconds : 0f));
            return _time - _arrivedAt > tail;
        }
    }

    /// <summary>
    /// Place the flight: where it starts and lands, how tall its caster is, and the textures it is drawn with.
    /// </summary>
    /// <param name="headContent">The head texture's content box, in texture pixels (measured once, one frame).</param>
    /// <param name="tint">The cast's colour (its Source glow), so the very first frame is drawn in it.</param>
    public void Place(Vector2 from, Vector2 to, float casterHeight, Color tint, Texture2D head, Rectangle headContent,
                      Texture2D trail, Texture2D glint, Texture2D spark, Texture2D shard, Texture2D flash)
    {
        _from = from;
        _tint = tint;
        _to = to;
        var d = to - from;
        _dir = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : Vector2.UnitX;
        _pos = from;
        _head = head; _trailTex = trail; _glint = glint; _spark = spark; _shard = shard; _flash = flash;
        _headScale = ProjectileMotion.HeadScale(headContent.Width, casterHeight, Look.HeadLength);
        _headLen = headContent.Width * _headScale;
        _headThick = headContent.Height * _headScale;
        _headOrigin = new Vector2(headContent.X + headContent.Width / 2f, headContent.Y + headContent.Height / 2f);
        _contactLife = ProjectileMotion.ContactLife(d.Length(), _headLen * Look.ContactReach);
        Placed = true;
    }

    /// <summary>
    /// Place a DRIVEN flight (ADR-011): the head is <paramref name="material"/> drawn at the thrower's own
    /// <paramref name="pixelScale"/> — so it is exactly as big in the air as in the hand — with
    /// <paramref name="edge"/> as its Source-coloured edge light. A performance then moves it with
    /// <see cref="Drive"/> and lands it with <see cref="Land"/>.
    /// </summary>
    /// <param name="to">Where its centre is at contact (the performance aims the tip at the target).</param>
    /// <param name="pixelScale">Arena pixels per texture pixel: the actor's draw scale.</param>
    /// <param name="padPx">The transparent margin around the object in its texture, per side, in texture pixels.</param>
    public void PlaceDriven(Vector2 from, Vector2 to, Color tint, Texture2D material, Texture2D edge, float pixelScale, int padPx,
                            Texture2D trail, Texture2D glint, Texture2D spark, Texture2D shard, Texture2D flash)
    {
        // (SPRAY's own path, byte for byte as it was: the pad decides the object's box)
        _from = from;
        _to = to;
        _tint = tint;
        var d = to - from;
        _dir = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : Vector2.UnitX;
        _pos = from;
        _material = material;
        _head = edge; _trailTex = trail; _glint = glint; _spark = spark; _shard = shard; _flash = flash;
        _headScale = pixelScale;
        _headLen = Math.Max(1, material.Width - 2 * padPx) * pixelScale;
        _headThick = Math.Max(1, material.Height - 2 * padPx) * pixelScale;
        _headOrigin = new Vector2(material.Width / 2f, material.Height / 2f);
        _contactLife = 1f;
        _driven = true;
        Placed = true;
    }

    /// <summary>
    /// Place a DRIVEN flight whose object fills <paramref name="content"/> of its texture (texture pixels): the head's
    /// length and thickness are that box's at <paramref name="pixelScale"/>, turned about its centre. For props drawn on a
    /// square canvas (the champion missiles, P1.4). The textures may be missing: the flight is still placed, driven and
    /// landed (its path is the presentation's truth), it just draws nothing.
    /// </summary>
    public void PlaceDriven(Vector2 from, Vector2 to, Color tint, Texture2D? material, Texture2D? edge, float pixelScale, Rectangle content,
                            Texture2D? trail, Texture2D? glint, Texture2D? spark, Texture2D? shard, Texture2D? flash)
    {
        _from = from;
        _to = to;
        _tint = tint;
        var d = to - from;
        _dir = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : Vector2.UnitX;
        _pos = from;
        _material = material;
        _head = edge; _trailTex = trail; _glint = glint; _spark = spark; _shard = shard; _flash = flash;
        _headScale = pixelScale;
        _headLen = Math.Max(1, content.Width) * pixelScale;
        _headThick = Math.Max(1, content.Height) * pixelScale;
        _headOrigin = new Vector2(content.X + content.Width / 2f, content.Y + content.Height / 2f);
        _contactLife = 1f;
        _driven = true;
        Placed = true;
    }

    /// <summary>True when a performance moves this head (<see cref="PlaceDriven"/>), not its own clock.</summary>
    public bool Driven => _driven;

    /// <summary>
    /// Move a DRIVEN head to <paramref name="position"/>, travelling along <paramref name="heading"/>, at
    /// <paramref name="u"/> (0..1) of its flight. It samples its trail and sheds its sparks exactly as a
    /// self-flying head does; it does NOT land itself — contact is the performance's decision (the beat).
    /// </summary>
    public void Drive(float dt, float u, Vector2 position, Vector2 heading)
    {
        if (!Placed) return;
        if (Landed) { Linger(dt); return; }
        _time += dt;
        var next = Math.Clamp(u, 0f, 1f);
        if (dt > 0f && next > _life) _lifeRate = (next - _life) / dt;
        _life = next;
        _pos = position;
        if (heading.LengthSquared() > 1e-6f) _dir = Vector2.Normalize(heading);
        _trail.Push(Rear(), _time);
        ShedSparks();
        Step(_sparks, dt, drag: 7f);
    }

    /// <summary>
    /// Advance the flight to <paramref name="life"/> (0..1 of the flight), sampling the trail. When the tip
    /// reaches the target (<see cref="ProjectileLook.ContactReach"/>) the flight lands by itself; after that
    /// this only lets the residue age.
    /// </summary>
    public void Fly(float dt, float life, Color tint)
    {
        if (!Placed) return;
        _tint = tint;
        if (Landed) { Linger(dt); return; }
        _time += dt;
        var next = Math.Clamp(life, 0f, 1f);
        if (dt > 0f && next > _life) _lifeRate = (next - _life) / dt;
        _life = next;
        _pos = Vector2.Lerp(_from, _to, ProjectileMotion.Ease(_life));
        _trail.Push(Rear(), _time);
        if (ProjectileMotion.Remaining(_pos, _to) <= _headLen * Look.ContactReach) { Land(); return; }
        ShedSparks();
        Step(_sparks, dt, drag: 7f);
    }

    /// <summary>SPARKS: 1-3 per flight, shed from the rear quarter, falling back relative to the travel.</summary>
    private void ShedSparks()
    {
        while (_sparksShed < SparkCount && _life >= ProjectileMotion.SparkLife(_sparksShed, SparkCount, _seed))
        {
            var j = _sparksShed++;
            var perp = new Vector2(-_dir.Y, _dir.X);
            var side = ProjectileMotion.Hash01(_seed, 400 + j) - 0.5f;
            _sparks[j] = new Particle
            {
                Position = Rear() + perp * side * _headThick * 0.8f,
                Velocity = -_dir * _headLen * 0.9f + perp * side * _headLen * 1.2f,
                Born = _time, Life = Look.SparkSeconds, Live = true,
            };
        }
    }

    /// <summary>
    /// The head has reached its target: end the flight and throw the directional impact. Called by
    /// <see cref="Fly"/> at contact, and by the player when the flight's clock ends first (a very short throw).
    /// </summary>
    public void Land()
    {
        if (!Placed || Landed) return;
        _arrivedAt = _time;
        for (var i = 0; i < ShardCount; i++)
        {
            var dir = ProjectileMotion.ShardDirection(i, ShardCount, Look.ImpactForward, Look.ImpactSpread, _dir, _seed);
            var forward = Vector2.Dot(dir, _dir) > 0.5f;
            var reach = _headLen * Look.ImpactReach * (forward ? 1f : 0.45f) * (0.8f + 0.4f * ProjectileMotion.Hash01(_seed, 500 + i));
            // v0 such that, under the drag below, the shard coasts ~reach before it stops
            _shards[i] = new Particle
            {
                Position = _to, Velocity = dir * reach * 9f,
                Born = _time, Life = Look.ImpactSeconds * (forward ? 1f : 0.8f), Live = true,
            };
        }
    }

    /// <summary>After landing: let the wake, sparks and shards finish.</summary>
    public void Linger(float dt)
    {
        _time += dt;
        Step(_sparks, dt, drag: 7f);
        Step(_shards, dt, drag: 9f);
    }

    /// <summary>The head's rear edge: where the trail is sampled, so it leaves the pommel and not the blade.</summary>
    private Vector2 Rear() => _pos - _dir * _headLen * 0.46f;

    private void Step(Particle[] ps, float dt, float drag)
    {
        var keep = MathF.Exp(-drag * dt);
        for (var i = 0; i < ps.Length; i++)
        {
            if (!ps[i].Live) continue;
            if (_time - ps[i].Born > ps[i].Life) { ps[i].Live = false; continue; }
            ps[i].Position += ps[i].Velocity * dt;
            ps[i].Velocity *= keep;
        }
    }

    /// <summary>
    /// Draw a material head (<see cref="ProjectileLook.MaterialKey"/>) into the caller's ALPHA-BLENDED batch: the
    /// object as it is, untinted. Nothing for a pure-energy head, or once it has landed.
    /// </summary>
    public void DrawMaterial(SpriteBatch b)
    {
        if (!Placed || Landed || _material is null) return;
        var angle = HeadAngle();
        b.Draw(_material, _pos, null, Color.White, angle, _headOrigin, _headScale, HeadFlip(), 0f);
    }

    private float HeadAngle() => MathF.Atan2(_dir.Y, _dir.X) + ProjectileMotion.Wobble(_time, Look.WobbleDegrees, Look.WobbleHz, _seed * 0.37f);

    private SpriteEffects HeadFlip() => _dir.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;

    /// <summary>Draw the composite into the caller's additive batch (<see cref="VfxBlend.PremultipliedAdditive"/>).</summary>
    public void Draw(SpriteBatch b)
    {
        LastSprites = 0;
        if (!Placed || _head is null || _trailTex is null) return;

        var hot = Color.Lerp(_tint, Color.White, Look.CoreWhite + 0.25f * PreImpact());
        // ONCE LANDED the trail has no head covering its front, so it must not end in a cut: it dims away
        // fast and its front tapers over a fraction of the head's length (else it leaves a flat block).
        var landed = Landed ? ProjectileMotion.Fall(_time - _arrivedAt, Look.LandedTrailSeconds, 1f) : 1f;
        var taper = MathF.Max(1f, _headLen * 0.35f);

        // 1. THE WAKE — longest and dimmest, with a small lateral wave that grows with age.
        var perp = new Vector2(-_dir.Y, _dir.X);
        var run = 0f;
        for (var k = 0; k + 1 < _trail.Count && landed > 0f; k++)
        {
            var a = _trail[k];
            var c = _trail[k + 1];
            var ageC = _time - c.Time;
            if (ageC > Look.WakeSeconds) break;
            var ageA = _time - a.Time;
            var pa = a.Position + perp * Wave(a, ageA);
            var pc = c.Position + perp * Wave(c, ageC);
            var mid = 0.5f * (ageA + ageC);
            var keep = ProjectileMotion.Fall(mid, Look.WakeSeconds, Look.WakeFalloff);
            run += Vector2.Distance(pa, pc);
            var front = Landed ? Math.Clamp(run / taper, 0f, 1f) : 1f;
            Segment(b, _trailTex, pc, pa, _headThick * Look.WakeWidth * MathF.Sqrt(keep), _tint * (Look.WakeBrightness * keep * landed * front));
        }

        // 2. THE CORE — short, narrow, hot, starting exactly at the head's rear.
        run = 0f;
        for (var k = 0; k + 1 < _trail.Count && landed > 0f; k++)
        {
            var a = _trail[k];
            var c = _trail[k + 1];
            var ageC = _time - c.Time;
            if (ageC > Look.CoreSeconds) break;
            var mid = 0.5f * ((_time - a.Time) + ageC);
            var keep = ProjectileMotion.Fall(mid, Look.CoreSeconds, 1f);
            var tight = Landed ? 1f : 1f - 0.25f * PreImpact();
            run += Vector2.Distance(a.Position, c.Position);
            var front = Landed ? Math.Clamp(run / taper, 0f, 1f) : 1f;
            Segment(b, _trailTex, c.Position, a.Position, _headThick * Look.CoreWidth * tight * keep, hot * (Look.CoreBrightness * keep * landed * front));
        }

        // 3. SPARKS — tiny, backward, gone fast.
        if (_spark is not null)
            foreach (ref readonly var s in _sparks.AsSpan())
            {
                if (!s.Live) continue;
                var keep = ProjectileMotion.Fall(_time - s.Born, s.Life, 1.5f);
                var size = _headLen * Look.SparkSize / _spark.Width;
                b.Draw(_spark, s.Position, null, VfxBlend.Light(Color.Lerp(_tint, Color.White, 0.5f) * keep), 0f,
                       new Vector2(_spark.Width / 2f, _spark.Height / 2f), size, SpriteEffects.None, 0f);
                LastSprites++;
            }

        if (!Landed)
        {
            // 4. THE HEAD — the gameplay element: one size, forward-readable, a small wobble.
            var angle = HeadAngle();
            var flip = HeadFlip();
            // LAUNCH: crisp at once, not a fade-in — 60 % on the frame it is placed, full on the next. A driven
            // head leaves a hand that was already holding it, so it is simply there.
            var appear = _driven ? 1f : Math.Clamp(0.6f + _time / 0.04f, 0f, 1f);
            for (var i = Look.AfterImages; i >= 1; i--)
            {
                var back = _pos - _dir * _headLen * 0.14f * i;
                b.Draw(_head, back, null, VfxBlend.Light(_tint * (appear * (i == 1 ? 0.35f : 0.16f))), angle, _headOrigin, _headScale, flip, 0f);
                LastSprites++;
            }
            // A material head's body is drawn by DrawMaterial; here only its EDGE is light, in the Source colour.
            var body = _material is null ? _tint * appear : Color.Lerp(_tint, Color.White, 0.3f) * (Look.EdgeBrightness * appear);
            b.Draw(_head, _pos, null, VfxBlend.Light(body), angle, _headOrigin, _headScale, flip, 0f);
            LastSprites++;
            // PRE-IMPACT: the last ~80 ms, a little extra light on the head, never a bigger head.
            var pre = PreImpact();
            if (pre > 0f)
            {
                b.Draw(_head, _pos, null, VfxBlend.Light(hot * (0.45f * pre)), angle, _headOrigin, _headScale, flip, 0f);
                LastSprites++;
            }

            // 5. THE GLINT — one bright point crossing the blade from rear to tip, once.
            var g = ProjectileMotion.GlintProgress(_life, Look.GlintStart, Look.GlintEnd);
            if (g >= 0f && _glint is not null)
            {
                var along = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var at = _pos + along * _headLen * MathHelper.Lerp(-0.28f, 0.34f, g);
                var shine = MathF.Sin(MathHelper.Pi * g);
                var white = VfxBlend.Light(Color.Lerp(_tint, Color.White, 0.75f) * shine);
                b.Draw(_glint, at, null, white, 0f,
                       new Vector2(_glint.Width / 2f, _glint.Height / 2f), _headLen * Look.GlintSize / _glint.Width * (0.7f + 0.3f * shine),
                       SpriteEffects.None, 0f);
                LastSprites++;
                // its hot centre: the star's rays are thin, so the point of light itself is a soft dot
                if (_spark is not null)
                {
                    b.Draw(_spark, at, null, white, 0f, new Vector2(_spark.Width / 2f, _spark.Height / 2f),
                           _headLen * Look.GlintSize * 0.35f / _spark.Width, SpriteEffects.None, 0f);
                    LastSprites++;
                }
            }
        }
        else
        {
            // 6. THE IMPACT — a hot instant at contact, then shards carrying the incoming direction.
            var since = _time - _arrivedAt;
            // THE CONTACT SLASH: the blade's force carried on through the target along the line it came in on,
            // thin, hot and gone in ~100 ms — sliding forward as it dies, never growing back.
            if (_trailTex is not null && Look.ImpactSlash > 0f && since < Look.ImpactSlashSeconds)
            {
                var k = since / Look.ImpactSlashSeconds;
                var keep = ProjectileMotion.Fall(since, Look.ImpactSlashSeconds, 1.2f);
                var tip = _to + _dir * _headLen * 0.5f;
                var start = tip - _dir * _headLen * Look.ImpactSlash * (0.35f - 0.3f * k);
                var end = tip + _dir * _headLen * Look.ImpactSlash * (0.25f + 0.45f * k);
                Segment(b, _trailTex, start, end, _headThick * 0.55f * (1f - 0.5f * k),
                        Color.Lerp(_tint, Color.White, 0.45f) * keep);
            }
            if (_flash is not null && since < Look.FlashSeconds)
            {
                var keep = ProjectileMotion.Fall(since, Look.FlashSeconds, 1.5f);
                b.Draw(_flash, _to, null, VfxBlend.Light(Color.Lerp(_tint, Color.White, 0.6f) * keep), 0f,
                       new Vector2(_flash.Width / 2f, _flash.Height / 2f), _headLen * Look.FlashSize / _flash.Width * (1f + 0.4f * (1f - keep)),
                       SpriteEffects.None, 0f);
                LastSprites++;
            }
            if (_shard is not null)
                foreach (ref readonly var s in _shards.AsSpan())
                {
                    if (!s.Live) continue;
                    var keep = ProjectileMotion.Fall(_time - s.Born, s.Life, 1.3f);
                    var rot = MathF.Atan2(s.Velocity.Y, s.Velocity.X);
                    var len = _headLen * Look.ShardLength * (0.6f + 0.4f * keep);
                    b.Draw(_shard, s.Position, null, VfxBlend.Light(Color.Lerp(_tint, Color.White, Look.ShardWhite) * keep), rot,
                           new Vector2(_shard.Width / 2f, _shard.Height / 2f),
                           new Vector2(len / _shard.Width, len * Look.ShardThickness / _shard.Height), SpriteEffects.None, 0f);
                    LastSprites++;
                }
        }
    }

    /// <summary>
    /// The pre-impact emphasis: 0 for the whole flight, rising to 1 across its last
    /// <see cref="ProjectileLook.PreImpactSeconds"/> before contact (a little light, never a bigger head).
    /// </summary>
    public float PreImpactEmphasis => PreImpact();

    private float PreImpact()
    {
        if (Landed || _lifeRate <= 0f) return 0f;
        var secondsLeft = (_contactLife - _life) / _lifeRate;
        return 1f - Math.Clamp(secondsLeft / MathF.Max(0.001f, Look.PreImpactSeconds), 0f, 1f);
    }

    private float Wave(in TrailSample s, float age)
        => _headLen * Look.WakeWave * Math.Clamp(age / Math.Max(0.001f, Look.WakeSeconds), 0f, 1f)
           * MathF.Sin(s.Time * 21f + _seed * 1.3f);

    private void Segment(SpriteBatch b, Texture2D tex, Vector2 from, Vector2 to, float width, Color colour)
    {
        var d = to - from;
        var len = d.Length();
        if (len < 0.5f || width < 0.3f) return;
        b.Draw(tex, from, null, VfxBlend.Light(colour), MathF.Atan2(d.Y, d.X), new Vector2(0f, tex.Height / 2f),
               new Vector2(len / tex.Width, width / tex.Height), SpriteEffects.None, 0f);
        LastSprites++;
    }
}
