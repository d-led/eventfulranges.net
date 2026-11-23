using System.Globalization;

namespace EventfulRanges.Tests;

/// <summary>
/// Runs a test under a specific culture and restores the previous one, so a
/// culture-sensitive formatting bug cannot hide behind the developer's locale.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo previousCulture;
    private readonly CultureInfo previousUiCulture;

    public CultureScope(string name)
    {
        previousCulture = CultureInfo.CurrentCulture;
        previousUiCulture = CultureInfo.CurrentUICulture;
        var culture = new CultureInfo(name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
    }
}
