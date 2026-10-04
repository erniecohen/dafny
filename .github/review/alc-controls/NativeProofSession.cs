using System.Runtime.Versioning;

namespace B3AlcGate;

/// <summary>Exactly six serial one-use permits. It cannot be constructed from an approval boolean.</summary>
[SupportedOSPlatform("linux")]
internal sealed class NativeProofSession {
  private static readonly string[] Names = ["baseline/true", "candidate/true", "baseline/reachable-false",
    "candidate/reachable-false", "baseline/fuel", "candidate/fuel"];
  private static readonly string[] Files = ["true.dfy", "true.dfy", "false.dfy", "false.dfy", "fuel.dfy", "fuel.dfy"];
  private static readonly string[] Hashes = ["bf2c7f7498a9221291dbc5244c3725f0fb9af93b741638595bdc11f3b83d24e6",
    "bf2c7f7498a9221291dbc5244c3725f0fb9af93b741638595bdc11f3b83d24e6",
    "a50e9615c8b49eefe55a458f6f4df613f11164406074b9a011f7f389aad2dc8e",
    "a50e9615c8b49eefe55a458f6f4df613f11164406074b9a011f7f389aad2dc8e",
    "55a2d36861839f955e222ab8b2ae6f838645ef013e8610b588f391bbe61c3206",
    "55a2d36861839f955e222ab8b2ae6f838645ef013e8610b588f391bbe61c3206"];
  private readonly object gate = new();
  private readonly NativeSmokeQualification qualification;
  private readonly ProductPin[] products;
  private readonly SharedCommonLibraries shared;
  private readonly string evidenceRoot;
  private int next;
  private Permit? current;
  private bool poisoned;
  private NativeProofSession(NativeSmokeQualification qualification, ProductPin[] products,
    SharedCommonLibraries shared, string evidenceRoot) {
    this.qualification = qualification; this.products = products.Select(p => p with { Files = p.Files.ToArray() }).ToArray();
    this.shared = shared; this.evidenceRoot = Path.GetFullPath(evidenceRoot);
  }
  public static NativeProofSession Create(NativeSmokeQualification qualification, ProductPin[] products,
    SharedCommonLibraries shared, string evidenceRoot) {
    qualification.MatchProducts(products); qualification.RecheckRuntime();
    shared.AssertAcceptable(); qualification.MatchShared(shared.Snapshot());
    return new(qualification, products, shared, evidenceRoot);
  }
  public Permit Admit(ProductPin product, string fixture, string[] arguments, IProofLifecycleSupervisor supervisor) {
    lock (gate) {
      try {
        R(!poisoned && current == null && next < 6, "fixed-six-session-admission");
        Boundary();
        var expectedProduct = products[next % 2];
        R(product.Label == expectedProduct.Label && product.SourceCommit == expectedProduct.SourceCommit &&
          product.Directory == expectedProduct.Directory && product.CoreSha256 == expectedProduct.CoreSha256 &&
          product.CoreInformationalVersion == expectedProduct.CoreInformationalVersion &&
          product.Files.SequenceEqual(expectedProduct.Files), "fixed-six-exact-product");
        var source = Path.Combine(AppContext.BaseDirectory, "proof-fixtures", Files[next]);
        R(fixture == source && Framework.Sha256(source) == Hashes[next], "fixed-six-exact-fixture");
        var folder = Path.Combine(evidenceRoot, Names[next].Replace('/', '-'));
        var expected = new[] { "verify", source, "--cores", "1", "--verification-time-limit", "20",
          "--resource-limit", "200000", "--solver-path", supervisor.WrapperPath,
          "--log-format", "json;LogFileName=" + Path.Combine(folder, "batches.json"),
          "--log-format", "csv;LogFileName=" + Path.Combine(folder, "batches.csv") };
        R(arguments.SequenceEqual(expected) && Path.IsPathFullyQualified(supervisor.WrapperPath),
          "fixed-six-exact-arguments-and-wrapper");
        current = new Permit(this, next, product, shared, supervisor, expected);
        return current;
      } catch { Poison("fixed-six-admission-failed"); throw; }
    }
  }
  public void AcceptCase(Permit permit) {
    lock (gate) {
      try {
        R(!poisoned && ReferenceEquals(current, permit) && permit.Consumed && permit.Ordinal == next,
          "fixed-six-case-completion");
        Boundary(); current = null; next++;
      } catch { Poison("fixed-six-case-completion-failed"); throw; }
    }
  }
  public bool Complete { get { lock (gate) { return !poisoned && current == null && next == 6; } } }
  public void Boundary() {
    lock (gate) {
      R(!poisoned, "fixed-six-session-poisoned");
      shared.AssertAcceptable(); qualification.RecheckRuntime(); qualification.MatchShared(shared.Snapshot());
      foreach (var product in products) { NativeProofSmokeControls.CheckPackage(product); }
      foreach (var index in Enumerable.Range(0, Files.Length)) {
        R(Framework.Sha256(Path.Combine(AppContext.BaseDirectory, "proof-fixtures", Files[index])) == Hashes[index],
          "fixed-six-fixture-changed");
      }
    }
  }
  public void Poison(string reason) {
    lock (gate) { poisoned = true; shared.Poison(reason); }
  }
  internal sealed class Permit {
    private readonly NativeProofSession owner;
    private readonly ProductPin product;
    private readonly SharedCommonLibraries shared;
    private readonly IProofLifecycleSupervisor supervisor;
    private readonly string[] arguments;
    internal int Ordinal { get; }
    internal bool Consumed { get; private set; }
    internal Permit(NativeProofSession owner, int ordinal, ProductPin product, SharedCommonLibraries shared,
      IProofLifecycleSupervisor supervisor, string[] arguments) {
      this.owner = owner; Ordinal = ordinal; this.product = product; this.shared = shared;
      this.supervisor = supervisor; this.arguments = arguments.ToArray();
    }
    public void Consume(ProductPin product, SharedCommonLibraries shared, string[] arguments,
      IProofLifecycleSupervisor supervisor, TimeSpan deadline) {
      lock (owner.gate) {
        try {
          R(!owner.poisoned && ReferenceEquals(owner.current, this) && !Consumed && owner.next == Ordinal &&
            ReferenceEquals(this.product, product) && ReferenceEquals(this.shared, shared) &&
            ReferenceEquals(this.supervisor, supervisor) && arguments.SequenceEqual(this.arguments) &&
            deadline == TimeSpan.FromSeconds(60), "fixed-six-unforgeable-one-use-call");
          owner.Boundary(); Consumed = true;
        } catch { owner.Poison("fixed-six-call-permit-failed"); throw; }
      }
    }
    public void BeforeInvocation() { try { owner.Boundary(); } catch { owner.Poison("fixed-six-pre-invocation-failed"); throw; } }
    public void Fail() => owner.Poison("fixed-six-native-invocation-failed");
  }
  private static void R(bool condition, string code) => NativeProofSmokeControls.Require(condition, code);
}
