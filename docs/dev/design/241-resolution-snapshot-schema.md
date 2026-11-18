# First closed resolution snapshot schema

This implements a deliberately small subset of the detached-result envelope in
[the pipeline design](241-pipeline.md#641-draft-resolution-result-envelope-version-1),
proposed by [PR 282](https://github.com/erniecohen/dafny/pull/282).
`--resolution-cache DIR` is hidden and opt-in, and requires
`--type-system-refresh`. It changes neither the module naming algorithm nor the
existing source-based `.doo` format. No general typed-body codec or type-checking
speedup is claimed by this subset.

## Eligibility and complete record inventory

The profile is `refreshed-direct-alias-default-plugins-v1`. The plugin list must
be exactly the two `DafnyOptions.DefaultPlugins`, in order and by identity. The
producer fingerprint includes the resolver assembly MVID, covering their code.
Custom, removed, reordered and duplicated plugins bypass lookup and publication.

Only an `AliasModuleDecl` with no attributes, no explicit export selectors and a
one-component target at root index zero is supported. Its target must be the
exact literal declaration in the current validated prerequisite closure, with no
copying import or selected copy view. Its unrestricted signature must also be its
default export, with no export sets, and must be the signature published by that
prerequisite. Error, partial and diagnostic-bearing prerequisite graphs bypass.
An already populated alias signature must equal this exact signature. Unsupported
views, abstract imports, literal module bodies, refinements, recursive groups and
all other node kinds use the ordinary resolver.

The canonical CBOR object table has exactly the following shape (array positions
are field identities; integers are unsigned):

```
["alias-reference-v1", 0, [
  [0, 1, alias-qualified-graph-identity, 1],
  [1, 2, target-local-key, target-qualified-graph-identity,
   target-signature-identity, "unrestricted-literal-signature"]
]]
```

Record 0 is the owned alias root; tag 1 has identity and target-reference fields.
Record 1 is an external reference; tag 2 selects a literal prerequisite's exact
root signature. The sole edge is 0 → 1. No owned type, body, generated signature,
class-member entry, diagnostic, builtin request, visibility scope, reporter,
whole-program receipt or runtime object is encoded. Those result collections
must be empty before this producer may publish. All source tokens, syntactic
alias fields and parent bindings come from this invocation's private parsed
alias, whose exact inputs are certified by its current local key. Source is not
stored in the payload and is never reparsed as a hit.

Materialization binds that alias's target declaration and signature to the exact
current prerequisite. As in ordinary alias resolution, the qualified identifier's
`Decl` is populated while its `Def` and `Sig` remain unset. The current compilation
supplies its error reporter, builtin publication view and translation-input
projection. No previous compilation's live object is retained. Literal bodies
still resolve normally on every invocation.

## Identity, validation and publication

The file is `<L>.<ctx>.dres`. The manifest records canonical effective options,
direct prerequisite roles/identities/keys, the exact producer and schema, payload
length/hash and the complete naming projection. The latter includes the owner,
every prerequisite in the semantic closure and each generated-signature selector,
with their effective prefixes. Any projection change selects another filename;
old contexts coexist. The filename, manifest and fresh projection must all agree.
Relocation of generated names is outside this implementation.

The documented `SnapshotModule4829`/`SnapshotModule51712` naming-table collision is
covered directly. Top-level resolver declarations actually have `_module.` in
their graph identities, so an end-to-end control additionally uses
`_module.SnapshotGraphModule6913`/`_module.SnapshotGraphModule30048`, whose six-character
digest prefixes collide. Both owner and prerequisite context changes are tested.

Validation regenerates the small canonical manifest and typed-reference table
from current inputs, without running the module resolver, and compares the exact
bytes. Thus a recomputed checksum cannot authorize altered tags, references or
facts. ZIP files have exactly `manifest.toml` then `module.cbor`. Before reading,
the archive is bounded to 3 MiB; each uncompressed entry is bounded to 1 MiB and
must have the exact expected length and EOF. Strings use canonical definite-length
CBOR text, limited to 65,535 UTF-8 bytes. Larger valid inputs bypass. Malformed,
truncated, duplicate, unknown, stale and inaccessible records are misses.

Publication accepts only the exact supported ordinary result. A private uniquely
named temporary file in the destination directory is flushed and atomically moved
without replacement. Equal concurrent writers validate the winner. An unequal or
corrupt winner disables the pair with a `.conflict` marker. Readers ignore temporary
files and refuse disabled pairs. Storage failures fall back to ordinary resolution;
cancellation propagates and temporary files are removed. Checksums do not establish
trust in an arbitrary writer; this remains a locally trusted producer namespace.

Current prepasses, whole-program rewrites, replacement and duplicate-name checks,
cardinality validation, ordered diagnostics and admission still run. CLI and IDE
use the same Core loader inside `ProgramResolver.ResolveModuleOutput`; opting in
bypasses the IDE's live-object cache while preserving outer pruning and coordination.
With the option omitted the existing cache profile remains in use.

## Evidence and performance boundary

`resolve.snapshot` profile events distinguish `hit`, `miss`, `rejected` and
`bypass`, with graph identity, local key, context and actual resolver-entry count.
The counter records ordinary module resolver calls and, for recursive groups,
the leader's group resolver call. A hit is only a successfully materialized alias,
not a skipped literal body or a proof-result cache hit. No extra event is emitted
with the option off.

Core controls compare fresh graphs and module Boogie, check identity rebinding,
options/source changes, both naming contexts, byte corruption including a new
checksum, oversized/truncated archives, unsupported views/plugins, cancellation,
atomic writers and conflicts. The integration control compares actual CLI output
and module Boogie across off/cold/warm/corrupt runs, with a proved lemma and an
`assert false` that must fail. The option-admission control rejects legacy mode
before parsing or creating cache/profile files.

Measurements must distinguish alias hit fraction from literal-body coverage and
report front-end resolution wall time separately from proof resource units.
Five cold/warm repetitions on Std and the eligible suite corpus are required;
small alias lookups do not establish a useful end-to-end speedup. Measurements
from private executors are not published in this repository.
