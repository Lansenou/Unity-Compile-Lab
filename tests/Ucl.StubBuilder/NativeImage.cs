using System.Buffers.Binary;

namespace Ucl.StubBuilder;

/// <summary>
/// A minimal unmanaged x86-64 DLL: DOS header, PE32+ headers with an empty CLI header directory, and one empty
/// <c>.rdata</c> section. Written byte by byte (PE/COFF specification) so no native toolchain is needed.
/// </summary>
internal static class NativeImage
{
    private const int PeOffset = 0x80;
    private const int OptionalHeaderSize = 240; // PE32+ with 16 data directories
    private const int FileAlignment = 0x200;
    private const int SectionAlignment = 0x1000;

    public static byte[] Build()
    {
        var image = new byte[FileAlignment * 2];
        var span = image.AsSpan();
        span[0] = (byte)'M';
        span[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(span[0x3C..], PeOffset);

        var pe = span[PeOffset..];
        pe[0] = (byte)'P';
        pe[1] = (byte)'E';
        var coff = pe[4..];
        BinaryPrimitives.WriteUInt16LittleEndian(coff, 0x8664); // AMD64
        BinaryPrimitives.WriteUInt16LittleEndian(coff[2..], 1); // sections
        BinaryPrimitives.WriteUInt16LittleEndian(coff[16..], OptionalHeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(coff[18..], 0x2022); // executable, large address aware, DLL

        var opt = coff[20..];
        BinaryPrimitives.WriteUInt16LittleEndian(opt, 0x20B); // PE32+
        BinaryPrimitives.WriteInt32LittleEndian(opt[4..], FileAlignment); // size of initialised data
        BinaryPrimitives.WriteInt64LittleEndian(opt[24..], 0x180000000); // image base
        BinaryPrimitives.WriteInt32LittleEndian(opt[32..], SectionAlignment);
        BinaryPrimitives.WriteInt32LittleEndian(opt[36..], FileAlignment);
        BinaryPrimitives.WriteUInt16LittleEndian(opt[40..], 6); // OS version 6.0
        BinaryPrimitives.WriteUInt16LittleEndian(opt[48..], 6); // subsystem version 6.0
        BinaryPrimitives.WriteInt32LittleEndian(opt[56..], SectionAlignment * 2); // size of image
        BinaryPrimitives.WriteInt32LittleEndian(opt[60..], FileAlignment); // size of headers
        BinaryPrimitives.WriteUInt16LittleEndian(opt[68..], 2); // Windows GUI subsystem
        BinaryPrimitives.WriteUInt16LittleEndian(opt[70..], 0x160); // high entropy VA, dynamic base, NX compatible
        BinaryPrimitives.WriteInt64LittleEndian(opt[72..], 0x100000); // stack reserve
        BinaryPrimitives.WriteInt64LittleEndian(opt[80..], 0x1000); // stack commit
        BinaryPrimitives.WriteInt64LittleEndian(opt[88..], 0x100000); // heap reserve
        BinaryPrimitives.WriteInt64LittleEndian(opt[96..], 0x1000); // heap commit
        BinaryPrimitives.WriteInt32LittleEndian(opt[108..], 16); // data directories, all empty: no COR20 header

        var section = opt[OptionalHeaderSize..];
        ".rdata"u8.CopyTo(section);
        BinaryPrimitives.WriteInt32LittleEndian(section[8..], 1); // virtual size
        BinaryPrimitives.WriteInt32LittleEndian(section[12..], SectionAlignment); // virtual address
        BinaryPrimitives.WriteInt32LittleEndian(section[16..], FileAlignment); // raw size
        BinaryPrimitives.WriteInt32LittleEndian(section[20..], FileAlignment); // raw pointer
        BinaryPrimitives.WriteUInt32LittleEndian(section[36..], 0x40000040); // initialised data, readable
        return image;
    }
}
