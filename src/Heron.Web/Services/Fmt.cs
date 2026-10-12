using System.Globalization;

namespace Heron.Web.Services;

// Small formatting helpers for the views, so every page writes money and times the same way.
public static class Fmt
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("en-GB");

    public static string Money(decimal value) => value.ToString("C2", Uk);
    public static string Money0(decimal value) => value.ToString("C0", Uk);
    public static string Date(DateTime value) => value.ToString("ddd d MMM yyyy", Uk);
    public static string Time(DateTime value) => value.ToString("HH:mm", Uk);
    public static string Number(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    // for style="width:..%" and for query strings, always with a dot
    public static string Iso(DateTime value) => value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    public static string IsoDate(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
