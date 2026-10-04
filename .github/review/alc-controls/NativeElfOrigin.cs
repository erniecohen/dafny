using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Text;
using static B3AlcGate.NativeProofSmokeControls;

namespace B3AlcGate;

internal sealed record NativeElfOriginReceipt(string Sha256, long Bytes, int ElfClass, string Endianness,
  int Machine, string Interpreter, string[] NeededLibraries, string[] RpathRunpath,
  string[] ForbiddenOriginStringsFound, string[] ForbiddenUndefinedSymbolsFound, int UndefinedSymbolCount,
  string UndefinedSymbolNamesSha256);

// Exact-image technical inspection, never execution. This scope is the pinned Linux
// x64 Z3 image and the two fixed launch modes, not arbitrary ELF transparency.
[SupportedOSPlatform("linux")]
internal static class NativeElfOrigin {
  internal const string SolverSha256 = "b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23";
  internal const string Z3SourceCommit = "0b6cdcdbc65da25ef0f73ac9da210574d0f66cf8";
  internal static readonly (string Path, string Sha256)[] SourceFiles = [
    ("src/shell/CMakeLists.txt", "824cdba928ed05ae6eec724bbf7105858a1cc70637285942882406bcc74380e2"),
    ("src/shell/main.cpp", "d8cbdc4696b0795bb3c0496df530acf11b6ceb2f593037b7a94bed9b5ac5c304"),
    ("src/shell/smtlib_frontend.cpp", "d53f4d5e9bbebeb09326e0774ea0e682bc1b8a237896c916614d0722adef45d0")
  ];
  private static readonly string[] Needed = ["libstdc++.so.6", "libm.so.6", "libgcc_s.so.1", "libc.so.6", "ld-linux-x86-64.so.2"];
  private static readonly string[] OriginStrings = ["/proc/self/exe", "$ORIGIN"];
  private static readonly string[] ForbiddenSymbols = ["readlink", "readlinkat", "dlopen", "dlmopen", "dlsym", "getauxval"];

  public static NativeElfOriginReceipt Inspect(byte[] image) {
    Require(Sha256(image) == SolverSha256, "exact-reviewed-z3-image-required");
    return new Reader(image).Inspect();
  }

  private sealed class Reader(byte[] image) {
    private sealed record Segment(uint Type, ulong Offset, ulong Address, ulong FileSize);
    private readonly List<Segment> segments = [];
    private int At(ulong offset, ulong count = 1) {
      Require(offset <= (ulong)image.Length && count <= (ulong)image.Length - offset, "elf-file-range-invalid");
      return checked((int)offset);
    }
    private ushort U16(ulong offset) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(At(offset, 2), 2));
    private uint U32(ulong offset) => BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(At(offset, 4), 4));
    private ulong U64(ulong offset) => BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(At(offset, 8), 8));
    private string CString(ulong offset, ulong available) {
      var start = At(offset, available);
      Require(available is > 0 and <= 67108864, "elf-string-table-bound");
      var maximum = (int)Math.Min(available, 65536);
      var length = image.AsSpan(start, maximum).IndexOf((byte)0);
      Require(length >= 0, "elf-unterminated-string");
      return new UTF8Encoding(false, true).GetString(image, start, length);
    }
    private ulong MapAddress(ulong address, ulong count) {
      var matches = segments.Where(s => s.Type == 1 && address >= s.Address &&
        address - s.Address <= s.FileSize && count <= s.FileSize - (address - s.Address)).ToArray();
      Require(matches.Length == 1, "elf-virtual-address-mapping-ambiguous");
      var offset = checked(matches[0].Offset + address - matches[0].Address);
      _ = At(offset, count);
      return offset;
    }

    public NativeElfOriginReceipt Inspect() {
      Require(image.Length >= 64 && image.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }) &&
        image[4] == 2 && image[5] == 1 && image[6] == 1 && U16(18) == 62 && U16(52) == 64 && U16(16) is 2 or 3,
        "elf64-little-endian-x64-required");
      var programOffset = U64(32);
      var programSize = U16(54);
      var programCount = U16(56);
      Require(programSize == 56 && programCount is > 0 and <= 128, "elf-program-header-bound");
      _ = At(programOffset, (ulong)programSize * programCount);
      for (var index = 0; index < programCount; index++) {
        var entry = checked(programOffset + (ulong)index * programSize);
        var segment = new Segment(U32(entry), U64(entry + 8), U64(entry + 16), U64(entry + 32));
        _ = At(segment.Offset, segment.FileSize);
        segments.Add(segment);
      }
      var interpreterEntry = segments.Single(s => s.Type == 3);
      var interpreter = CString(interpreterEntry.Offset, interpreterEntry.FileSize);
      Require(interpreter == "/lib64/ld-linux-x86-64.so.2", "reviewed-elf-interpreter-required");
      var dynamic = segments.Single(s => s.Type == 2);
      Require(dynamic.FileSize % 16 == 0 && dynamic.FileSize / 16 <= 4096, "elf-dynamic-table-bound");
      var values = new List<(ulong Tag, ulong Value)>();
      var ended = false;
      for (ulong index = 0; index < dynamic.FileSize / 16; index++) {
        var entry = checked(dynamic.Offset + 16 * index);
        var tag = U64(entry);
        if (tag == 0) { ended = true; break; }
        values.Add((tag, U64(entry + 8)));
      }
      Require(ended, "elf-dynamic-table-unterminated");
      var stringSize = values.Single(v => v.Tag == 10).Value; // DT_STRSZ
      var stringAddress = values.Single(v => v.Tag == 5).Value; // DT_STRTAB
      var stringOffset = MapAddress(stringAddress, stringSize);
      string DynamicString(ulong offset) {
        Require(offset < stringSize, "elf-dynamic-string-outside-table");
        return CString(checked(stringOffset + offset), stringSize - offset);
      }
      var needed = values.Where(v => v.Tag == 1).Select(v => DynamicString(v.Value)).ToArray();
      var search = values.Where(v => v.Tag is 15 or 29).Select(v => DynamicString(v.Value)).ToArray();
      Require(needed.SequenceEqual(Needed) && search.Length == 0, "reviewed-native-dependencies-no-rpath-required");

      // The pinned image retains a DYNSYM section. Refuse a stripped/alternate image
      // without that structural evidence, rather than infer absent symbols from it.
      var sectionOffset = U64(40);
      var sectionSize = U16(58);
      var sectionCount = U16(60);
      Require(sectionSize == 64 && sectionCount is > 0 and <= 2048, "elf-section-header-bound");
      _ = At(sectionOffset, (ulong)sectionSize * sectionCount);
      var symbolSections = new List<ulong>();
      for (var index = 0; index < sectionCount; index++) {
        var section = checked(sectionOffset + (ulong)index * sectionSize);
        if (U32(section + 4) == 11) { symbolSections.Add(section); }
      }
      Require(symbolSections.Count == 1, "exact-elf-dynsym-section-required");
      var symbols = symbolSections[0];
      var symbolOffset = U64(symbols + 24);
      var symbolSize = U64(symbols + 32);
      Require(U64(symbols + 56) == 24 && symbolSize % 24 == 0 && symbolSize / 24 <= 1048576,
        "elf-dynsym-entry-bound");
      _ = At(symbolOffset, symbolSize);
      var link = U32(symbols + 40);
      Require(link < sectionCount, "elf-dynsym-string-link-invalid");
      var linked = checked(sectionOffset + (ulong)link * sectionSize);
      Require(U32(linked + 4) == 3 && U64(linked + 24) == stringOffset && U64(linked + 32) == stringSize,
        "elf-dynsym-dynamic-string-table-mismatch");
      var undefined = new List<string>();
      for (ulong index = 0; index < symbolSize / 24; index++) {
        var symbol = checked(symbolOffset + index * 24);
        var name = U32(symbol);
        if (U16(symbol + 6) == 0 && name != 0) { undefined.Add(DynamicString(name)); }
      }
      var forbidden = undefined.Where(n => ForbiddenSymbols.Contains(n.Split('@')[0], StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
      var strings = OriginStrings.Where(value => image.AsSpan().IndexOf(Encoding.ASCII.GetBytes(value)) >= 0).ToArray();
      Require(forbidden.Length == 0 && strings.Length == 0, "native-executable-origin-resource-indicators-found");
      var names = Encoding.UTF8.GetBytes(string.Join("\n", undefined.Order(StringComparer.Ordinal)) + "\n");
      return new(Sha256(image), image.Length, 64, "little", 62, interpreter, needed, search, strings,
        forbidden, undefined.Count, Sha256(names));
    }
  }
}
