using System.Xml;
using System.Xml.Linq;

/// <summary>Checks exact covered/valid line counts in a merged Cobertura report; missing or empty data fails closed.</summary>
internal static class CoverageGate
{
    internal static int Check(string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[1], out var minimum) || minimum is < 0 or > 100)
        {
            Console.Error.WriteLine("usage: verify --coverage <cobertura.xml> <minimum-line-percent>");
            return 2;
        }

        try
        {
            var root = XDocument.Load(args[0]).Root;
            if (root?.Name != "coverage"
                || !long.TryParse(root.Attribute("lines-covered")?.Value, out var covered)
                || !long.TryParse(root.Attribute("lines-valid")?.Value, out var total)
                || total <= 0 || covered < 0 || covered > total)
            {
                Console.Error.WriteLine("coverage: missing or invalid line counts");
                return 2;
            }

            var percent = covered * 100m / total;
            Console.WriteLine(FormattableString.Invariant($"coverage: {covered}/{total} lines ({percent:F2}%), minimum {minimum}%"));
            return percent >= minimum ? 0 : 1;
        }
        catch (Exception e) when (e is IOException or XmlException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("coverage: report is unreadable");
            return 2;
        }
    }
}
