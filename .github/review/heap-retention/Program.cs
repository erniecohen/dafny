using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Graphs;

// This is a cut of the observed graph, not an estimate obtained by summing type sizes.
// No object addresses, process metadata or root-label strings are written to the report.
internal sealed class Retention {
  private readonly MemoryGraph graph;
  private readonly BitArray baseline;
  public long RootedBytes { get; }
  public int NullNodes { get; private set; }

  public Retention(MemoryGraph graph) {
    this.graph = graph;
    Require((int)graph.RootIndex >= 0 && graph.RootIndex < graph.NodeIndexLimit, "missing graph root");
    baseline = Walk([(int)graph.RootIndex], []);
    RootedBytes = Bytes(baseline);
    Require(RootedBytes > 0, "empty rooted heap");
  }

  public static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidDataException(message); }
  }

  public static Retention Read(string path) {
    var dump = new GCHeapDump(path);
    CheckSampling(dump.AverageCountMultiplier, dump.AverageSizeMultiplier, dump.CountMultipliersByType);
    return new Retention(dump.MemoryGraph);
  }

  internal static void CheckSampling(float count, float size, float[]? byType) {
    Require(count == 1 && size == 1 && (byType == null || byType.All(x => x == 1)),
      "sampled graph cannot establish retention");
  }

  private BitArray Walk(IEnumerable<int> roots, HashSet<int> blocked) {
    var seen = new BitArray((int)graph.NodeIndexLimit);
    var todo = new Queue<int>();
    foreach (var root in roots) {
      Require(root >= 0 && root < seen.Length, "invalid node index");
      if (!blocked.Contains(root) && !seen[root]) { seen[root] = true; todo.Enqueue(root); }
    }
    var storage = graph.AllocNodeStorage();
    while (todo.TryDequeue(out var id)) {
      var node = graph.GetNode((NodeIndex)id, storage);
      if (!node.Defined) {
        // The collector can emit a null stack-root target. It has no bytes or children.
        Require(graph.GetAddress((NodeIndex)id) == 0, "undefined non-null node; graph is incomplete");
        NullNodes = 1;
        continue;
      }
      Require(node.Size >= 0, "negative node size");
      for (var child = node.GetFirstChildIndex(); child != NodeIndex.Invalid; child = node.GetNextChildIndex()) {
        var index = (int)child;
        Require(index >= 0 && index < seen.Length, "invalid child index");
        if (!blocked.Contains(index) && !seen[index]) {
          seen[index] = true;
          todo.Enqueue(index);
        }
      }
    }
    return seen;
  }

  private long Bytes(BitArray set) {
    long sum = 0;
    var storage = graph.AllocNodeStorage();
    for (var i = 0; i < set.Length; i++) {
      if (set[i]) { sum = checked(sum + graph.GetNode((NodeIndex)i, storage).Size); }
    }
    return sum;
  }

  public sealed record Cut(int SelectedNodes, long SelectedShallowBytes, long ReachableBytes,
    long RetainedBytes, long SharedBytes);

  internal Cut Measure(HashSet<int> selected) {
    Require(selected.Count > 0, "no rooted nodes selected; measurement is vacuous");
    Require(selected.All(i => i >= 0 && i < baseline.Length && baseline[i]), "selected node is not rooted");
    Require(!selected.Contains((int)graph.RootIndex), "cannot cut the synthetic graph root");
    var descendants = Walk(selected, []);
    var remaining = Walk([(int)graph.RootIndex], selected);
    var reachable = Bytes(descendants);
    var retained = RootedBytes - Bytes(remaining);
    long shallow = 0;
    var storage = graph.AllocNodeStorage();
    foreach (var id in selected) { shallow += graph.GetNode((NodeIndex)id, storage).Size; }
    Require(retained >= shallow && retained <= reachable, "inconsistent graph cut");
    return new Cut(selected.Count, shallow, reachable, retained, reachable - retained);
  }

  public Cut Measure(string pattern) {
    var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    var selected = new HashSet<int>();
    var storage = graph.AllocNodeStorage();
    var typeStorage = graph.AllocTypeNodeStorage();
    for (var i = 0; i < baseline.Length; i++) {
      if (!baseline[i]) { continue; }
      var node = graph.GetNode((NodeIndex)i, storage);
      if (node.Defined && regex.IsMatch(node.GetType(typeStorage).Name)) { selected.Add(i); }
    }
    return Measure(selected);
  }
}

internal static class Controls {
  private static void Equal<T>(T expected, T actual, string message) {
    Retention.Require(EqualityComparer<T>.Default.Equals(expected, actual), "control: " + message);
  }

  private static void Reject(Action action, string message, string diagnostic) {
    try { action(); } catch (InvalidDataException e) when (e.Message.Contains(diagnostic)) { return; }
    throw new InvalidDataException("control: accepted " + message);
  }

  internal static MemoryGraph Fixture(bool dangling = false, bool nullRoot = false) {
    var g = new MemoryGraph(16);
    // R -> A -> shared, private -> cycle -> private
    // R -> B -> shared. The disconnected node must not become an owner.
    var edges = new[] { new[] {1, 2}, new[] {3, 4}, new[] {3}, Array.Empty<int>(),
      new[] {5}, new[] {4}, Array.Empty<int>() };
    var names = new[] {"root", "ownerA", "ownerB", "shared", "private", "cycle", "disconnected"};
    var sizes = new[] {0, 3, 5, 7, 11, 13, 17};
    var ids = sizes.Select((_, i) => g.GetNodeIndex((ulong)(i + 1) * 16)).ToArray();
    for (var i = 0; i < ids.Length; i++) {
      var children = new System.Collections.Generic.GrowableArray<NodeIndex>(4);
      foreach (var child in edges[i]) { children.Add(ids[child]); }
      if (i == 0 && (dangling || nullRoot)) { children.Add(g.GetNodeIndex(dangling ? 1024UL : 0UL)); }
      g.SetNode(ids[i], g.CreateType(names[i], "control", sizes[i]), sizes[i], children);
    }
    g.RootIndex = ids[0];
    g.AllowReading();
    return g;
  }

  public static void Run() {
    var graph = Fixture();
    var r = new Retention(graph);
    Equal(39L, r.RootedBytes, "rooted total excludes disconnected node");
    Equal(new Retention.Cut(1, 3, 34, 27, 7), r.Measure("^ownerA$"), "shared cycle cut");
    Equal(new Retention.Cut(1, 5, 12, 5, 7), r.Measure("^ownerB$"), "other owner keeps shared bytes");
    Equal(new Retention.Cut(2, 8, 39, 39, 0), r.Measure("^owner[AB]$"), "collective cut is not sum of cuts");
    Equal(new Retention.Cut(2, 14, 34, 27, 7), r.Measure([1, 4]), "nested selection counts once");
    Reject(() => r.Measure("absent"), "empty selector", "no rooted nodes selected");
    Reject(() => r.Measure("disconnected"), "unrooted selector", "no rooted nodes selected");
    Reject(() => r.Measure([6]), "unrooted node", "selected node is not rooted");
    Reject(() => r.Measure([0]), "graph root", "cannot cut the synthetic graph root");
    Reject(() => new Retention(Fixture(dangling: true)), "incomplete graph", "undefined non-null node");
    Equal(39L, new Retention(Fixture(nullRoot: true)).RootedBytes, "null root target has no size");
    Reject(() => Retention.CheckSampling(2, 1, null), "count sampling", "sampled graph");
    Reject(() => Retention.CheckSampling(1, 2, null), "size sampling", "sampled graph");
    Reject(() => Retention.CheckSampling(1, 1, [1, 2]), "per-type sampling", "sampled graph");
    // Exercise the actual pinned gcdump writer/reader, not only the in-memory algorithm.
    var path = Path.GetTempFileName();
    try {
      GCHeapDump.WriteMemoryGraph(graph, path, "retention control");
      Equal(r.Measure("^ownerA$"), Retention.Read(path).Measure("^ownerA$"), "gcdump round trip");
      // The pinned format has no public sampled-graph writer. Change only the fixture's
      // metadata through its internal setters, then use the real serializer and entry point.
      var sampled = new GCHeapDump(path);
      typeof(GCHeapDump).GetProperty("AverageCountMultiplier")!.SetValue(sampled, 2f);
      typeof(GCHeapDump).GetMethod("Write", System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance)!.Invoke(sampled, [path]);
      Reject(() => Retention.Read(path), "sampled file", "sampled graph");
    } finally { File.Delete(path); }
    Console.WriteLine("heap retention: 16 controls passed");
  }
}

internal static class Program {
  public static int Main(string[] args) {
    try {
      if (args.SequenceEqual(["--self-test"])) { Controls.Run(); return 0; }
      if (args.Length < 2) { throw new ArgumentException("usage: HeapRetention <dump.gcdump> <type-regex> [...]"); }
      var retention = Retention.Read(args[0]);
      var cuts = args.Skip(1).Select(pattern => new { pattern, cut = retention.Measure(pattern) }).ToArray();
      using var input = File.OpenRead(args[0]);
      Console.WriteLine(JsonSerializer.Serialize(new {
        schema = 1, metric = "observed-graph-node-cut", observedGraphOnly = true,
        inputSha256 = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(),
        rootedBytes = retention.RootedBytes, nullNodes = retention.NullNodes, cuts
      }, new JsonSerializerOptions { WriteIndented = true }));
      return 0;
    } catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) {
      Console.Error.WriteLine("incomplete retention evidence: " + e.Message);
      return 1;
    }
  }
}
