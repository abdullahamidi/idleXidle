namespace IdleXIdle.Game;

/// <summary>
/// WHO HAS THE PLAYER'S EYES THIS FRAME — one tier per kind of surface, ascending.
/// </summary>
/// <remarks>
/// <para>
/// The game must not ask the player to look at two things at once. When more than one surface could
/// speak, the highest tier owns the frame and everything below it waits: it does not paint, and its
/// clock does not burn, so it lands when the owner hands the frame back rather than expiring unseen.
/// </para>
/// <para>
/// The host computes the owner once per Update from the flags it already holds and keeps it in one
/// field; every paint gate and every clock under it reads that field and asks one question — "is
/// anything above ME up?" This is not a framework: a tier is a name for a rank in one comparison,
/// and the ranking is the enum's order, so it is written here and nowhere else.
/// </para>
/// </remarks>
public enum AttentionOwner
{
    /// <summary>Nothing has a claim; the page is the whole picture.</summary>
    None,

    /// <summary>Transient direct-action feedback: a notice toast, a locked-tile refusal, the COPIED line.</summary>
    Feedback,

    /// <summary>The coach is lighting a control — a spotlight, brackets and a card the player is meant to read.</summary>
    Coach,

    /// <summary>The Expedition Log is open: a full-screen read the player chose.</summary>
    Report,

    /// <summary>The death transition is on the page: the collapse, the black, the stage coming back.</summary>
    Death,

    /// <summary>A chest reveal: the one moment the game asks the player to stop and look.</summary>
    Reveal,

    /// <summary>A production modal or confirmation: the title, settings, help, a tour, the welcome, an attunement, the vault's stall, a SELL / SALVAGE question.</summary>
    Modal,

    /// <summary>The authored opening is running: nothing else speaks.</summary>
    Opening,
}
