# Quickstart

Take real-valued ranges from nothing to a working program in five minutes. One
package, one console project, no services.

## What you need

- .NET 10 SDK: `dotnet --version` prints `10.x`.

## 1. Create a project

```shell
dotnet new console -o ranges-demo
cd ranges-demo
dotnet add package EventfulRanges --version 0.0.1
```

If `0.0.1` is not on nuget.org yet, clone the repository and add the project
reference instead:

```shell
dotnet add reference ../eventfulranges.net/src/EventfulRanges/EventfulRanges.csproj
```

## 2. Cut a range out of a booking

Replace `Program.cs` with this:

```csharp
using EventfulRanges;

var booked = new Interval(1, 10);
var cancelled = new Interval(3, 5);

foreach (var part in booked.Subtract(cancelled))
{
    Console.WriteLine(part);
}
```

```shell
dotnet run
```

```text
[1,3)
(5,10]
```

Two pieces survive, and their bounds are open exactly where the cancellation
cut into the booking.

## 3. Ask two more questions

Append this to `Program.cs`:

```csharp
var shared = Interval.Intersection(new Interval(1, 10), new Interval(8, 20));
Console.WriteLine(shared);

var busy = Intervals.Union(
    booked.Subtract(cancelled),
    new[] { new Interval(7, 12) });

foreach (var part in busy)
{
    Console.WriteLine(part);
}
```

```text
[8,10]
[1,3)
(5,12]
```

`Union` merges the two ranges that touch the new one and leaves the island
alone. Nothing was sorted by hand.

## 4. Check a single value

Still in `Program.cs`:

```csharp
Console.WriteLine(Intervals.Contains(busy, 4));   // False: the cancelled gap
Console.WriteLine(Intervals.Contains(busy, 9));   // True
```

`Contains` is a static method on `Intervals`; an `Interval` also has an instance
`Contains` for a single range.

## What you just did

Every answer came from comparing endpoints. No endpoint was ever added,
subtracted or averaged, so no floating-point error could accumulate — the
cancelled gap is a gap, not "close enough". [Range algebra](../explanation/range-algebra.md)
explains why that is the whole design.

## Where to go next

- [Model a calendar of busy days](../how-to/model-a-calendar.md) — dates as day
  numbers, bookings, cancellations.
- [API reference](../reference/api.md) — every type, method, default and
  exception.
- Status: `0.0.1` ships interval algebra only. The operation log, replicas and
  the n-dimensional half arrive in `0.0.x`; `0.1.0` is parity.
