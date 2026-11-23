using System.Collections.Immutable;
using System.Text;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The metadata join is a CRDT join: commutative, associative and idempotent,
/// so replicas that merged the same metadata converge whatever order it
/// arrived in.
/// </summary>
public sealed class MetaTests
{
    [Fact]
    public void A_key_carried_by_only_one_side_survives()
    {
        Union("""{"color":"#ff0000"}""", """{"author":"alice"}""")
            .ShouldBe("""{"author":"alice","color":"#ff0000"}""");
    }

    [Fact]
    public void Equal_values_collapse_to_one_key()
    {
        Union("""{"color":"#ff0000"}""", """{"color":"#ff0000"}""")
            .ShouldBe("""{"color":"#ff0000"}""");
    }

    [Fact]
    public void The_larger_raw_value_wins_whatever_the_argument_order()
    {
        var red = Raw("""{"color":"#ff0000"}""");
        var blue = Raw("""{"color":"#0000ff"}""");

        Meta.Union(red, blue).ShouldBe(Meta.Union(blue, red));
        Text(Meta.Union(red, blue)).ShouldBe("""{"color":"#ff0000"}""");
    }

    [Fact]
    public void Numbers_compare_as_text_so_nine_beats_ten()
    {
        Union("""{"v":9}""", """{"v":10}""").ShouldBe("""{"v":9}""");
    }

    [Fact]
    public void Nested_objects_are_joined_key_by_key()
    {
        Union("""{"style":{"weight":2,"dash":false}}""", """{"style":{"color":"#ff0000"}}""")
            .ShouldBe("""{"style":{"color":"#ff0000","dash":false,"weight":2}}""");
    }

    [Fact]
    public void A_side_that_is_not_an_object_yields_the_other_side()
    {
        var metadata = Raw("""{"color":"#ff0000"}""");

        Meta.Union(default, metadata).ShouldBe(metadata);
        Meta.Union(metadata, default).ShouldBe(metadata);
        Meta.Union(Raw("\"red\""), metadata).ShouldBe(metadata);
        Meta.Union(metadata, Raw("\"red\"")).ShouldBe(metadata);
    }

    [Fact]
    public void Two_missing_sides_stay_missing()
    {
        Meta.Union(default, default).IsDefaultOrEmpty.ShouldBeTrue();
    }

    [Fact]
    public void The_join_is_commutative_idempotent_and_associative()
    {
        var a = Raw("""{"a":1,"nested":{"x":1}}""");
        var b = Raw("""{"b":2,"nested":{"y":2}}""");
        var c = Raw("""{"c":3,"nested":{"x":9}}""");

        Meta.Union(a, b).ShouldBe(Meta.Union(b, a));
        Meta.Union(Meta.Union(a, b), a).ShouldBe(Meta.Union(a, b));
        Meta.Union(Meta.Union(a, b), c).ShouldBe(Meta.Union(a, Meta.Union(b, c)));
    }

    private static string Union(string a, string b) => Text(Meta.Union(Raw(a), Raw(b)));

    private static ImmutableArray<byte> Raw(string json) =>
        ImmutableArray.Create(Encoding.UTF8.GetBytes(json));

    private static string Text(ImmutableArray<byte> raw) => Encoding.UTF8.GetString(raw.AsSpan());
}
