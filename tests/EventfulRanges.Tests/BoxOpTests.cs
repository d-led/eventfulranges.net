using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// A box operation is the n-dimensional counterpart of a range operation: the same
/// shapes, plus retraction, which cancels one specific edit instead of removing a
/// region from the set.
/// </summary>
public sealed class BoxOpTests
{
    [Fact]
    public void Each_kind_has_the_wire_name_the_log_uses()
    {
        BoxOpKind.Add.ToWireName().ShouldBe("add");
        BoxOpKind.Remove.ToWireName().ShouldBe("remove");
        BoxOpKind.Retract.ToWireName().ShouldBe("retract");
        Should.Throw<ArgumentOutOfRangeException>(() => ((BoxOpKind)7).ToWireName());
    }

    [Fact]
    public void A_wire_name_parses_back_to_its_kind()
    {
        BoxOpKindNames.TryParse("add", out var add).ShouldBeTrue();
        add.ShouldBe(BoxOpKind.Add);
        BoxOpKindNames.TryParse("retract", out var retract).ShouldBeTrue();
        retract.ShouldBe(BoxOpKind.Retract);
        BoxOpKindNames.TryParse("paint", out _).ShouldBeFalse();
    }

    [Fact]
    public void A_well_formed_operation_has_no_complaint()
    {
        ValidOperation(BoxOpKind.Add).Validate().ShouldBe(BoxOpError.None);
        ValidOperation(BoxOpKind.Remove).Validate().ShouldBe(BoxOpError.None);
        new BoxOp("a", BoxOpKind.Retract, Region(), Ref: "target").Validate().ShouldBe(BoxOpError.None);
    }

    [Fact]
    public void Validation_names_the_reason_an_operation_is_malformed()
    {
        new BoxOp(string.Empty, BoxOpKind.Add, Region()).Validate().ShouldBe(BoxOpError.MissingId);
        new BoxOp("a", BoxOpKind.Retract, Region()).Validate().ShouldBe(BoxOpError.MissingRetractTarget);
        new BoxOp("a", (BoxOpKind)7, Region()).Validate().ShouldBe(BoxOpError.UnknownKind);
        new BoxOp("a", BoxOpKind.Add, new Box([5], [1])).Validate().ShouldBe(BoxOpError.InvalidBox);
        new BoxOp("a", BoxOpKind.Add, new Box([1], [1])).Validate().ShouldBe(BoxOpError.EmptyBox);
    }

    [Fact]
    public void A_malformed_operation_is_rejected_where_a_well_formed_one_is_required()
    {
        var malformed = new BoxOp("a", BoxOpKind.Retract, Region());

        var thrown = Should.Throw<InvalidBoxOpException>(malformed.EnsureValid);
        thrown.Error.ShouldBe(BoxOpError.MissingRetractTarget);
    }

    [Fact]
    public void The_factories_build_the_shape_the_log_records()
    {
        var added = BoxOp.Add([0, 0], [10, 10]);
        added.Kind.ShouldBe(BoxOpKind.Add);
        added.Box.ShouldBe(new Box([0, 0], [10, 10]));
        added.Id.ShouldNotBeNullOrWhiteSpace();

        var removed = BoxOp.RemoveWithBounds([0], [10], [Bound.Open], [Bound.Closed]);
        removed.Kind.ShouldBe(BoxOpKind.Remove);
        removed.Box.ShouldBe(new Box([0], [10], [Bound.Open], [Bound.Closed]));
    }

    [Fact]
    public void A_retraction_names_its_target_and_carries_the_same_region()
    {
        var retraction = BoxOp.Retract("target-1", Region());

        retraction.Kind.ShouldBe(BoxOpKind.Retract);
        retraction.Ref.ShouldBe("target-1");
        retraction.Box.ShouldBe(Region());
        retraction.Validate().ShouldBe(BoxOpError.None);
    }

    [Fact]
    public void Identifiers_are_unique()
    {
        var ids = Enumerable.Range(0, 200).Select(_ => BoxOp.Add([0], [1]).Id).ToList();

        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void An_operation_renders_its_kind_and_region()
    {
        new BoxOp("a", BoxOpKind.Add, new Box([0, 2], [10, 5])).ToString()
            .ShouldBe("add [0 10) x [2 5)");
    }

    private static Box Region() => new([1, 2], [5, 6]);

    private static BoxOp ValidOperation(BoxOpKind kind) => new("a", kind, Region());
}
