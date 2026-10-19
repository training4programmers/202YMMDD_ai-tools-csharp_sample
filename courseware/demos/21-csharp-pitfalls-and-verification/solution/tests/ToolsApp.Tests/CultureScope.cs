using System.Globalization;

namespace ToolsApp.Tests;

/// <summary>
/// Runs a block of test code under another culture, then restores the original. Culture bugs only
/// show up on machines that aren't set to en-US, so the tests bring those machines to the build.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _original = CultureInfo.CurrentCulture;

    public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _original;
}
