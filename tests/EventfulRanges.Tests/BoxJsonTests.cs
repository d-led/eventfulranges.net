using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// A box reaches the log as its two corners, its faces only where they differ from
/// the half-open default, and its metadata verbatim — so a replica in either
/// language reads back exactly what was written.
/// </summary>
public sealed class BoxJsonTests
{
    [Fact]
    public void A_box_round_trips_through_json()
    {
        var box = new Box([0, 0], [1, 1], [Bound.Open, Bound.Closed], [Bound.Closed, Bound.Open])
            .WithMeta(Raw("""{"color":"red"}"""));

        var back = JsonSerializer.Deserialize<Box>(JsonSerializer.Serialize(box));

        back.ShouldNotBeNull();
        back.ShouldBe(box);
        Text(back.Meta).ShouldBe("""{"color":"red"}""");
    }

    [Fact]
    public void A_half_open_box_stays_in_its_compact_form()
    {
        var json = JsonSerializer.Serialize(new Box([0], [1]));

        json.ShouldNotContain("bound", Case.Insensitive);
        json.ShouldNotContain("meta", Case.Insensitive);

        var back = JsonSerializer.Deserialize<Box>(json);
        back.ShouldNotBeNull();
        back.Contains([0]).ShouldBeTrue();
        back.Contains([1]).ShouldBeFalse();
    }

    [Fact]
    public void The_corners_and_the_faces_are_the_wire_shape()
    {
        var json = JsonSerializer.Serialize(
            new Box([0, 2], [10, 5], [Bound.Closed, Bound.Open], [Bound.Open, Bound.Closed]));

        json.ShouldBe(
            """{"min":[0,2],"max":[10,5],"minBound":["closed","open"],"maxBound":["open","closed"]}""");
    }

    [Fact]
    public void An_unknown_face_is_rejected_rather_than_guessed()
    {
        var json = """{"min":[0],"max":[1],"minBound":["ajar"],"maxBound":["open"]}""";

        var thrown = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Box>(json));

        thrown.Message.ShouldContain("ajar");
    }

    [Fact]
    public void A_box_operation_round_trips_with_its_region()
    {
        var op = new BoxOp("a", BoxOpKind.Retract, new Box([0, 0], [1, 1]), 7, Ref: "target", Origin: "replica-1");

        var back = JsonSerializer.Deserialize<BoxOp>(JsonSerializer.Serialize(op));

        back.ShouldBe(op);
        back!.Ref.ShouldBe("target");
        back.Origin.ShouldBe("replica-1");
    }

    [Fact]
    public void The_operation_kind_is_written_by_name()
    {
        var json = JsonSerializer.Serialize(new BoxOp("a", BoxOpKind.Retract, new Box([0], [1]), Ref: "t"));

        json.ShouldContain(""""kind":"retract"""");
    }

    private static ImmutableArray<byte> Raw(string json) =>
        ImmutableArray.Create(Encoding.UTF8.GetBytes(json));

    private static string Text(ImmutableArray<byte> raw) => Encoding.UTF8.GetString(raw.AsSpan());
}
