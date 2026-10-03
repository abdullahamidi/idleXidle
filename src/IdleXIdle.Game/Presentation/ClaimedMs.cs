using System;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// THE CLAIM HOOK (design.md section 4, SHIELD and RETURN): the fight milliseconds a recipe has said it presents a
/// ShieldGained or a Heal at, with its own picture and cue, so the generic receive gives way there. A fixed array,
/// cleared per wave; nothing is allocated after construction.
/// </summary>
/// <remarks>
/// When the table is full the oldest claim is overwritten: claims are made just ahead of the event they name, so the
/// oldest one has already been read.
/// </remarks>
public sealed class ClaimedMs
{
    private readonly int[] _ms;
    private int _count;
    private int _next;

    /// <summary>A table holding up to <paramref name="capacity"/> claims.</summary>
    public ClaimedMs(int capacity = 16)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _ms = new int[capacity];
    }

    /// <summary>How many claims are held.</summary>
    public int Count => _count;

    /// <summary>Claim the fight millisecond <paramref name="atMs"/> (a second claim of the same ms is the same claim).</summary>
    public void Claim(int atMs)
    {
        if (IsClaimed(atMs)) return;
        _ms[_next] = atMs;
        _next = (_next + 1) % _ms.Length;
        if (_count < _ms.Length) _count++;
    }

    /// <summary>True when a recipe claimed <paramref name="atMs"/>.</summary>
    public bool IsClaimed(int atMs)
    {
        for (var i = 0; i < _count; i++)
            if (_ms[i] == atMs) return true;
        return false;
    }

    /// <summary>Forget every claim (a new wave: its clock restarts at 0).</summary>
    public void Clear()
    {
        _count = 0;
        _next = 0;
    }
}
