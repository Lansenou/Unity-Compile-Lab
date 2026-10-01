// Original source of a stand-in third-party package DLL for the ucl fixtures. Apache-2.0.
// It is compiled against a System.Runtime contract, so its type references point at System.Runtime 4.0.0.0.

namespace Vendor.Contracts
{
    /// <summary>A score entry, as a small serialisation library would ship it.</summary>
    public class ScoreEntry
    {
        /// <summary>Player name.</summary>
        public string Player { get; set; }

        /// <summary>Points.</summary>
        public int Points { get; set; }

        /// <summary>Formats the entry.</summary>
        public override string ToString() => string.Concat(Player, ": ", Points);
    }
}
