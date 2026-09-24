using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The fall loop's build snapshot folds the woven skills' hash codes into one number. It used
/// <c>Enumerable.Sum</c>, which is CHECKED: string hashes are random per process, two of them overflow a
/// quarter of the time, and the hunter's first fall threw an OverflowException out of Update (0.1.0-alpha).
/// </summary>
public class FallSnapshotTest
{
    [Fact]
    public void test_fall_snapshot_hash_fold_wraps_instead_of_throwing()
    {
        // Arrange: the values Enumerable.Sum refuses
        var hashes = new[] { int.MaxValue, int.MaxValue, 1 };

        // Act
        var sum = Game1.WrappingSum(hashes);

        // Assert: the wrapped two's-complement sum, the same number for the same build every time
        Assert.Equal(unchecked(int.MaxValue + int.MaxValue + 1), sum);
        Assert.Equal(sum, Game1.WrappingSum(hashes));
    }
}
