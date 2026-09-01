namespace IdleXIdle.Core.Sources;

/// <summary>
/// The six SOURCES — what power is drawn from. A skill's chosen variation attaches one; regions,
/// creatures, item elements and the set system all speak this same six-word vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// THE ENUM ORDER IS THE RING: each Source is strong against the next two and weak against the
/// previous two (see <see cref="SourceMatchup"/>), so there is no best element by construction.
/// Persisted BY NAME in saves (item elements, chest elements, saved skill sources) — never rename
/// a member without a save migration.
/// </para>
/// <para>
/// Lived in <c>Core.Automation</c> from the creature era until 2026-08-31; it was never an
/// automation concept, and the refactor gave it a home of its own.
/// </para>
/// </remarks>
public enum Source { Body, Mind, Nature, Machine, Shadow, Spirit }
