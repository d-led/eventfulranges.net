using System.Text.Json;
using KurrentDB.Client;
using Shouldly;

namespace EventfulRanges.Kurrent.Tests;

/// <summary>
/// The KurrentDB log against a real server. Every test writes to its own pair of
/// streams, so a shared server never makes them order-dependent.
/// </summary>
public sealed class KurrentLogTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [KurrentFact]
    public async Task A_fresh_log_reads_back_empty_and_holds_no_snapshot()
    {
        using var log = OpenIsolated();

        var read = await log.ReadAsync(0);

        read.Ops.ShouldBeEmpty();
        read.Version.ShouldBe(0);
        await Should.ThrowAsync<SnapshotNotFoundException>(async () => await log.LoadSnapshotAsync());
    }

    [KurrentFact]
    public async Task Appended_operations_read_back_in_order_with_the_log_length()
    {
        using var log = OpenIsolated();
        var first = Op.Add(1, 5);
        var second = Op.Remove(3, 4);

        await log.AppendAsync(0, [first, second]);

        var read = await log.ReadAsync(0);
        read.Ops.ShouldBe([first, second]);
        read.Version.ShouldBe(2);
    }

    [KurrentFact]
    public async Task Reading_from_a_version_returns_only_what_follows_it()
    {
        using var log = OpenIsolated();
        await log.AppendAsync(0, [Op.Add(0, 1), Op.Add(1, 2), Op.Add(2, 3)]);

        var tail = await log.ReadAsync(2);

        tail.Ops.ShouldHaveSingleItem().Interval.ShouldBe(new Interval(2, 3));
        tail.Version.ShouldBe(3);
    }

    [KurrentFact]
    public async Task Reading_past_the_end_still_reports_the_log_length()
    {
        using var log = OpenIsolated();
        await log.AppendAsync(0, [Op.Add(0, 1), Op.Add(1, 2)]);

        var past = await log.ReadAsync(9);

        past.Ops.ShouldBeEmpty();
        past.Version.ShouldBe(2);
    }

    [KurrentFact]
    public async Task Reading_past_the_end_of_a_missing_stream_reports_an_empty_log()
    {
        using var log = OpenIsolated();

        var past = await log.ReadAsync(9);

        past.Ops.ShouldBeEmpty();
        past.Version.ShouldBe(0);
    }

    [KurrentFact]
    public async Task A_stale_expected_version_conflicts_instead_of_appending()
    {
        using var log = OpenIsolated();
        await log.AppendAsync(0, [Op.Add(0, 1)]);

        var conflict = await Should.ThrowAsync<StoreConflictException>(
            async () => await log.AppendAsync(0, [Op.Add(2, 3)]));

        conflict.ExpectedVersion.ShouldBe(0);
        conflict.ActualVersion.ShouldBe(1);
        (await log.ReadAsync(0)).Ops.ShouldHaveSingleItem();
    }

    [KurrentFact]
    public async Task A_second_connection_sees_what_the_first_one_wrote()
    {
        using var writer = OpenIsolated();
        var written = new[] { Op.Add(0, 1), Op.Add(1, 2) };
        await writer.AppendAsync(0, written);

        using var reader = KurrentLog.Open(KurrentFactAttribute.Connection!, writer.OperationStream, writer.SnapshotStream);

        var read = await reader.ReadAsync(0);
        read.Ops.ShouldBe(written);
        read.Version.ShouldBe(2);
    }

    [KurrentFact]
    public async Task A_snapshot_round_trips_with_the_version_it_covers()
    {
        using var log = OpenIsolated();
        await log.AppendAsync(0, [Op.Add(0, 1)]);

        await log.SaveSnapshotAsync([1, 2, 3], 1);

        var snapshot = await log.LoadSnapshotAsync();
        snapshot.Data.ShouldBe([1, 2, 3]);
        snapshot.Version.ShouldBe(1);
    }

    [KurrentFact]
    public async Task The_newest_snapshot_is_the_one_loaded()
    {
        using var log = OpenIsolated();

        await log.SaveSnapshotAsync([1], 1);
        await log.SaveSnapshotAsync([2], 2);

        var snapshot = await log.LoadSnapshotAsync();
        snapshot.Version.ShouldBe(2);
        snapshot.Data.ShouldBe([2]);
    }

    [KurrentFact]
    public async Task A_malformed_operation_payload_fails_the_read()
    {
        using var log = OpenIsolated();
        using var client = Connect();
        await AppendRaw(client, log.OperationStream, KurrentLog.OperationEventType, "{not-json"u8.ToArray(), null);

        var failure = await Should.ThrowAsync<JsonException>(async () => await log.ReadAsync(0));

        failure.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [KurrentFact]
    public async Task A_snapshot_with_malformed_metadata_fails_the_load()
    {
        using var log = OpenIsolated();
        using var client = Connect();
        await AppendRaw(client, log.SnapshotStream, KurrentLog.SnapshotEventType, "{}"u8.ToArray(), "{not-json"u8.ToArray());

        await Should.ThrowAsync<JsonException>(async () => await log.LoadSnapshotAsync());
    }

    [KurrentFact]
    public async Task A_cancelled_token_fails_the_read_and_the_snapshot_load()
    {
        using var log = OpenIsolated();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await log.ReadAsync(0, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await log.LoadSnapshotAsync(cancelled.Token));
    }

    [KurrentFact]
    public async Task A_log_opens_with_the_stream_names_it_was_given()
    {
        var suffix = Guid.NewGuid().ToString("n");
        using var log = KurrentLog.Open(
            KurrentFactAttribute.Connection!,
            $"custom-ops-{suffix}",
            $"custom-snapshots-{suffix}");

        log.OperationStream.ShouldBe($"custom-ops-{suffix}");
        log.SnapshotStream.ShouldBe($"custom-snapshots-{suffix}");
        await log.AppendAsync(0, [Op.Add(0, 1)]);
        (await log.ReadAsync(0)).Version.ShouldBe(1);
    }

    private static KurrentLog OpenIsolated()
    {
        var suffix = Guid.NewGuid().ToString("n");
        return KurrentLog.Open(KurrentFactAttribute.Connection!, $"itest-ops-{suffix}", $"itest-snap-{suffix}");
    }

    private static KurrentDBClient Connect() =>
        new(KurrentDBClientSettings.Create(KurrentFactAttribute.Connection!));

    private static async Task AppendRaw(
        KurrentDBClient client,
        string stream,
        string eventType,
        byte[] data,
        byte[]? metadata)
    {
        await client.AppendToStreamAsync(
            stream,
            StreamState.Any,
            [new EventData(Uuid.NewUuid(), eventType, data, metadata, "application/json")],
            cancellationToken: new CancellationTokenSource(Timeout).Token);
    }
}
