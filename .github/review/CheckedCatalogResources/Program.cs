using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

// Read the actual managed resource directory, without loading or executing the assembly.
if (args.Length != 2) {
  throw new ArgumentException("usage: CheckedCatalogResources PIPELINE_DLL OUTPUT_DIRECTORY");
}
using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
var metadata = pe.GetMetadataReader();
var directory = pe.PEHeaders.CorHeader!.ResourcesDirectory;
var section = pe.GetSectionData(directory.RelativeVirtualAddress);
Directory.CreateDirectory(args[1]);
var names = new List<string>();
foreach (var handle in metadata.ManifestResources) {
  var resource = metadata.GetManifestResource(handle);
  var name = metadata.GetString(resource.Name);
  if (!name.StartsWith("DafnyStandardLibraries-checked", StringComparison.Ordinal) ||
      !name.EndsWith(".doo", StringComparison.Ordinal)) {
    continue;
  }
  if (!resource.Implementation.IsNil || name != Path.GetFileName(name)) {
    throw new InvalidDataException("Checked catalog must be embedded: " + name);
  }
  var offset = checked((int)resource.Offset);
  var reader = section.GetReader(offset, section.Length - offset);
  var length = reader.ReadInt32();
  if (length <= 0) {
    throw new InvalidDataException("Empty checked catalog resource: " + name);
  }
  File.WriteAllBytes(Path.Combine(args[1], name), reader.ReadBytes(length));
  names.Add(name);
}
Console.WriteLine(JsonSerializer.Serialize(names.Order()));
