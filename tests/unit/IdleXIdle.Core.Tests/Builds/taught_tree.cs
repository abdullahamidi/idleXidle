using System.Linq;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A mastery tree that has learned every skill — what most fixtures mean by "a champion".
/// </summary>
/// <remarks>
/// <para>
/// All twelve skills are learned on the mastery tree since 2026-08-30, when the designer retired
/// composing a skill from a Source and a Form: "Artık source ve form skill oluşturmamın bir önemi
/// kalmadı. Zaten mastery treeden yürüdüğüm yolda skill açacağım." A fixture handing
/// <c>BuildComposer.Compose</c> a bare <see cref="MasteryTree"/> now weaves NOTHING, which is correct
/// and is exactly what fifty-eight tests said when the gate went in.
/// </para>
/// <para>
/// Most of those tests are not about the gate at all — they are about beats, cadence, haul, slot
/// kinds — and what they always meant was "a champion who has these skills". This says that out loud
/// instead of relying on skills being free. The gate itself is held by
/// <c>test_a_skill_the_tree_has_not_taught_cannot_be_chosen</c>, which uses a BARE tree on purpose.
/// </para>
/// <para>
/// It takes the ROAD nodes only, not the specialisations that gate them — <see cref="MasteryTree.RestoreTaken"/>
/// does not walk prerequisites, and a specialisation also grants a trigger and an Affinity, which
/// would quietly turn every fixture into a champion wearing six enchantments.
/// </para>
/// </remarks>
internal static class Taught
{
    internal static MasteryTree Everything()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        tree.RestoreTaken(MasteryCatalog.Nodes
            .Where(n => n.Kind == MasteryKind.SkillRoad)
            .Select(n => n.Id));
        return tree;
    }
}
