// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

namespace Game.Tools
{
    // Not under Game.Core and not referencing it: no UFX001 here, but the global generator still runs.
    public static class BadMath
    {
        public static int Seed => Example.Json.Generated.JsonContextMarker.Value;
    }
}
