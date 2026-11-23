using Shouldly;
using Xunit;

namespace EventfulRanges.Tests;

/// <summary>
/// The box stream is what makes an n-dimensional replica outlive its process: one
/// operation per line, read back in the order it was accepted.
/// </summary>
public sealed class BoxJsonlLogTests
{
    [Fact]
    public async Task Writing_then_reopening_gives_back_the_same_log()
    {
        using var directory = new TemporaryDirectory();
        var first = BoxJsonlLog.Open(directory.DirectoryPath);
        var ops = new[] { BoxOp.Add([0, 0], [1, 1]) with { Id = "a" }, BoxOp.Remove([2], [3]) with { Id = "b" } };

        await first.AppendAsync(0, ops);

        var reopened = BoxJsonlLog.Open(directory.DirectoryPath);
        var read = await reopened.ReadAsync(0);

        read.Version.ShouldBe(2);
        read.Ops.ShouldBe(ops);
    }

    [Fact]
    public async Task Reading_from_a_version_returns_only_what_follows_it()
    {
        var log = BoxJsonlLog.Open(new TemporaryDirectory().DirectoryPath);
        await log.AppendAsync(0, [BoxOp.Add([0], [1]) with { Id = "a" }, BoxOp.Add([2], [3]) with { Id = "b" }]);

        var read = await log.ReadAsync(1);

        read.Ops.ShouldHaveSingleItem().Id.ShouldBe("b");
        read.Version.ShouldBe(2);
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_and_writes_nothing()
    {
        using var directory = new TemporaryDirectory();
        var log = BoxJsonlLog.Open(directory.DirectoryPath);
        await log.AppendAsync(0, [BoxOp.Add([0], [1]) with { Id = "a" }]);

        var thrown = await Should.ThrowAsync<StoreConflictException>(
            async () => await log.AppendAsync(0, [BoxOp.Add([2], [3])]));

        thrown.ExpectedVersion.ShouldBe(0);
        thrown.ActualVersion.ShouldBe(1);
        (await log.ReadAsync(0)).Ops.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_stream_holds_one_operation_per_line()
    {
        using var directory = new TemporaryDirectory();
        var log = BoxJsonlLog.Open(directory.DirectoryPath);

        await log.AppendAsync(0, [BoxOp.Add([0, 0], [1, 1]), BoxOp.Add([2], [3])]);

        var lines = await File.ReadAllLinesAsync(log.FilePath);
        lines.Length.ShouldBe(2);
        lines[0].ShouldContain("\"kind\":\"add\"");
        lines[0].ShouldContain("\"min\":[0,0]");
    }

    [Fact]
    public void An_empty_directory_is_an_empty_log()
    {
        using var directory = new TemporaryDirectory();

        var log = BoxJsonlLog.Open(directory.DirectoryPath);

        log.Version.ShouldBe(0);
    }
}
