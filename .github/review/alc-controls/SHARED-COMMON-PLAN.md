> Historical strict-availability plan retained for review. This draft uses the
> separate contract in UNAVAILABLE-METADATA-PLAN.md; it does not pass the strict
> complete-availability contract below. The external strict checkpoint is unchanged.

# Separate common-library native smoke draft

This is source only. No build, managed product execution, solver execution, native
proof, runtime ownership control, or new unload control has run on this draft.
It is based on source `a6132c6758c863ec4a897e4ac1534dc727f0b006`. All 19 files
qualified by the prior fixed lifecycle gate remain byte-identical, including
`Isolation.cs`, `Run.cs`, `Program.cs`, the supervisor, and the byte-forwarding
wrapper. Their existing private-all-library audits are not weakened or repurposed.

The strict full-availability preflight presently has an unresolved dependency:
the exact common System.Reactive DLL declares WindowsRuntime, while the pinned
packages and .NET 8 framework source do not provide it. The historical runtime
receipt did not serialize the actual TPA catalog, so the source inference and
future authoritative TPA check remain distinct. The code keeps the strict missing
reference rejection. This checkpoint must not be submitted as a runnable proof
gate or described as fully available; a separately reviewed proposal is needed
before changing that guarantee. Other supported native API designs remain open.

The earlier all-private native smoke failed on ordinary Boogie plugin creation.
In .NET 8.0.31, `Assembly.LoadFrom(path)` explicitly calls
`AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath)`. Contextual reflection
does not redirect that API. Boogie's ordinary `ProverFactory.Load` uses precisely
that API for `Boogie.Provers.SMTLib.dll`; duplicate private and Default definitions
of its factory types cannot satisfy the cast. No supported API changes that fixed
path API into a collectible-context load while preserving this unchanged call.
The relevant public references are the
[exact runtime source](https://github.com/dotnet/runtime/blob/v8.0.31/src/libraries/System.Private.CoreLib/src/System/Reflection/Assembly.cs#L327-L353)
and the [managed loading API table](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/loading-managed).

The separate draft instead shares one exact common managed library closure in
Default and keeps all Dafny assemblies and other product dependencies private.
Returning the same Assembly objects is a supported dependency-sharing pattern;
the [ALC documentation](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
explains the resulting type identity. This is a different isolation scope, with
persistent Boogie state. It is not fresh Boogie state and does not establish
native query or resource parity.

## Fixed preflight and common closure

Before the first product context can exist, the fixed smoke API checks the same
original baseline archive, candidate source/build inventory, solver image and
origin evidence, inspected lifecycle receipt, old 19 source hashes, six fixture
source hashes, and explicit privileged Linux x64 cgroup prerequisites. The
original baseline source remains `b07c038737d6713b6d1a5848d7568bdc972de7dd`, archive
SHA256 `679b3569a3e188cdea5d9c061a463ef4adab849584c7d6990434bd87f80f1bae`,
and Core SHA256 `9e4eaf6a52cc7ed29d8df5e2185ce00032382a9e9be54688bbcfda2262c865b9`.
There is no substitution of baseline, compiler, default flags, or query/cost data.

`SharedCommonLibraries` first records the actual framework-only host. It reads
the exact TPA files in the runtime directory and records full metadata identities,
SHA256, lengths, paths, and actual Default Assembly object ownership. App-supplied
TPA helpers, product TPAs, assembly duplicates, dynamic or unknown Default
assemblies, and a missing framework catalog fail. The harness has one explicit
separate allowed object and hash.

The common closure starts with the fixed 13 reviewed, byte-identical Boogie DLLs.
The source contains their exact hashes. Both complete package inventories must
contain precisely those root filenames. Bounded PE metadata inspection walks
every common assembly's AssemblyRefs before loading any common product code.
Each reference must resolve to either an exact TPA definition with compatible
name, culture, token and version, or a package file selected by each product's
ordinary `AssemblyDependencyResolver`. Both resolvers must choose the same
relative file, metadata identity, length and bytes. Every common reference must
agree with the already selected target; conflicting resolution fails. A direct
root-file fallback is used only when the resolver supplies no mapping, matching
the existing package loader. No unqualified filename or prefix waiver is used.

This matters for platform-specific package assets. The pinned packages contain
both root and platform variants of several System DLLs. A forced root-only
common closure would not preserve an ordinary resolver's Linux asset choice.
The draft records that exact resolver choice rather than treating all same-name
files as interchangeable. All package files, including unselected variants,
remain subject to complete package hashing before each run.

The compatibility check permits an actual resolved assembly version greater than
or equal to a metadata reference version, following the supported ALC cache
version rule. Name, culture, public-key token and content type must agree. This is
a binding compatibility rule, not permission to choose a new library: the actual
resolved definition, canonical path, SHA256 and exact Assembly object remain
frozen in the preflight catalog and all audits. An unavailable or incompatible
version is still rejected; neither package nor framework is upgraded here.

Missing, different, unreviewed, malformed, unsupported, non-neutral, escaped,
symlinked, or identity-incompatible common dependencies fail before product load.
No common library may reference a Dafny assembly or the harness. Framework trust
ends at the precise TPA catalog; common managed recursion cannot silently fall
back to the host or another package. Bounds are 64 common files, 512 framework
files, 256 references per file, 64 MiB per managed image, 32 Default audit
snapshots, and the existing 4,096-file complete package bound. These are safety
limits, not changed Dafny or solver limits.
For six successful runs there are exactly 28 audits: initial, metadata preflight,
common preload, then before/loader/collection/evidence for each of six runs, and
one final audit. The 32-audit safety bound therefore covers the entire sequence.

After the entire metadata closure passes, each common assembly is preloaded once
in Default from the baseline resolver-selected canonical path. A strictly owned
Default resolver returns only these already pinned common files or exact TPA
files. It captures no private context, private type, option or delegate. Common
dependencies are the same objects for both products. Default has no collectible
product dependency. Boogie's unchanged plugin `LoadFrom` therefore targets the
already loaded canonical SMTLib object and its exact factory/type dependencies.
There is no binary patch, source patch, factory-cache mutation, static reset,
private event-field surgery, or prover-option workaround.

## Private product and invocation invariants

`SharedProductContext` is separate from `ProductContext`. It returns the exact
recorded common or framework Assembly objects for shared references. Everything
else must resolve to an explicitly inventoried file in that product package and
load in that one collectible context. Its final audit rejects a private copy of
any common or TPA assembly, checks the exact private DafnyCore identity, version
and hash, and records string-only loader evidence. Preloaded common entries have
their own `shared-common-loaded` kind: they do not pretend every file was directly
requested by Dafny. Actual private-loader requests use `shared-common`.

Every Default audit accepts only the harness, exact TPA objects, and this precise
common closure. It rechecks each loaded file's path, identity and SHA256 and
compares actual Assembly object identity with all prior observations. The receipt
uses stable object-slot numbers for these source-enforced reference-equality
checks. New framework assemblies may load only from the preflight TPA catalog;
previous objects cannot disappear or be replaced. After preload, the complete
common object set must remain present. A generic Boogie/System prefix exemption
would be a bug. Default Dafny loading is always rejected.

`SharedNativeRuns` invokes the same ordinary public static
`DafnyBackwardsCompatibleCli.MainWithWriters(TextWriter,TextWriter,TextReader,string[])`
and requires the shared framework `Task<int>` result. It keeps the original fixed
six sequence: baseline/candidate true, reachable false, then Fuel. Each pair uses
the same exact source path and source hash. Native budgets are explicitly cores 1,
verification time 20 seconds, resource limit 200,000, plus a 60-second invocation
safety deadline. These smoke budgets are not the later whole-suite default gate.
Backend, seed, solver flags, generated queries and raw resource replies are never
rewritten. The transparent wrapper and exclusive ownership supervisor are the
unchanged qualified implementations.

The supervisor records and drains all owned solver launches after ordinary CLI
output/log completion. Then the actual private Dafny scheduler is disposed; no
engine-disposal claim is invented. Private reflection/task/exception handles
remain inside a no-inline frame, and only detached strings, numeric evidence and
a long weak reference leave it. Actual weak collection must complete before the
next product runs. Surviving shared callbacks, tasks or cached private options are
detected by that requirement, not excused. Any invocation, ownership, scheduler,
loader, weak-collection or framework-witness failure poisons the fixed host and
stops the sequence.

The prior `NativeProofEvidence` parser stays byte-identical. All six cases still
require nonzero actual proof batches, proof commands, raw SMT replies, resources,
source/routine matches, expected 0/4 CLI exits, strict true/Fuel Valid outcomes,
and an actual reachable-false Invalid outcome plus diagnostic. Unknown/error and
zero-batch controls fail. Native reset/VC groups and their final raw resource
replies must match the untouched JSON batch multiset; no sums, differences or
normalization can manufacture a match. Raw input/output streams, completion and
cleanup receipts remain hash-bound. The unchanged origin evidence is for the
exact Z3 5.1.0 image and fixed stdin SMT modes, not arbitrary executable support.

## Persistent-state limit and next gates

The exact common Default objects are intentionally noncollectible. Boogie
statics, counters, caches, naming sets and any other native shared state persist
between products. None is cleared or made to look fresh. CoreCLR string,
BigInteger and HashCode seed witnesses stay in one shared framework process, but
that alone does not prove identical queries or resource counts. The native source
contains state such as AST IDs, request/split/process counters, log-name sets and
engine dictionaries. Their existence is a reason for additional controls; this
draft does not assert they necessarily alter this six-case SMT traffic.

The new main receipt scope is
`prototype/six-fixed-shared-common-boogie-native-proof-smoke-controls` and carries
the full common metadata closure, exact framework catalog, all Default audits,
complete original/source fixture and product pins, and explicit fields
`persistentBoogieState=true`, `freshBoogieStateEstablished=false`, and
`nativeQueryOrResourceParityEstablished=false`. The common receipt makes the same
limits explicit. An original private-all-library receipt must never be relabeled
or accepted through a general audit waiver.

The fixed CI coordinator needs only the exact new receipt scope and strict common
receipt inspection in addition to its existing preflight, six-case denominators,
parent-owned cgroup backstop, and source/harness hashes. The separate
`shared-common-receipt-validation.py` contains that proposed inspection; the old
coordinator has not been edited or run here. Its four nonproof private-all controls
and their old source audits remain unchanged.

Source review comes first. After reviewed source is frozen, any runtime smoke
qualification must still demonstrate actual SMT/proof batches, zero owned
processes, collection of each private Dafny context, and exact shared object
audits. Only then can repeat, interleaved and reversed-order native controls
assess shared-state/order dependence. Exact full-query and resource parity gates
are separate mandatory work. Passing six controls must not be reported as full
B3 acceptance, fresh native Boogie state, unprivileged deployment, or a full
native cost denominator.
