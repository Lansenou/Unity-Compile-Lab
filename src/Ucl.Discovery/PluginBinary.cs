using System.Buffers.Binary;

namespace Ucl.Discovery;

/// <summary>Tells a managed plugin DLL from an unmanaged (native) one by its PE headers, never by its path or name.</summary>
public static class PluginBinary
{
    /// <summary>How many leading bytes <see cref="IsManaged"/> needs: the DOS stub and PE headers.</summary>
    public const int HeaderBytes = 4096;

    private const int CliHeaderDirectory = 14;

    /// <summary>
    /// True when <paramref name="headers"/> (the start of a file) is a PE image whose CLI header directory (the COR20
    /// header, data directory 14 of the optional header; PE/COFF specification) is present: a .NET assembly. A native
    /// DLL, or a file that is not a PE image, is false.
    /// </summary>
    public static bool IsManaged(byte[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var b = headers.AsSpan();
        if (b.Length < 0x40 || b[0] != 'M' || b[1] != 'Z')
        {
            return false;
        }

        var pe = BinaryPrimitives.ReadInt32LittleEndian(b[0x3C..]);
        if (pe < 0 || pe > b.Length - 26 || b[pe] != 'P' || b[pe + 1] != 'E' || b[pe + 2] != 0 || b[pe + 3] != 0)
        {
            return false;
        }

        var optional = pe + 24;
        var (directoryCount, directories) = BinaryPrimitives.ReadUInt16LittleEndian(b[optional..]) switch
        {
            0x10B => (optional + 92, optional + 96), // PE32
            0x20B => (optional + 108, optional + 112), // PE32+
            _ => (-1, -1),
        };
        var cli = directories + (CliHeaderDirectory * 8);
        return directoryCount >= 0
            && cli + 8 <= b.Length
            && BinaryPrimitives.ReadInt32LittleEndian(b[directoryCount..]) > CliHeaderDirectory
            && BinaryPrimitives.ReadInt32LittleEndian(b[(cli + 4)..]) > 0;
    }
}
