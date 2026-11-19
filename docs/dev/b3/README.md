# B3 P0: baseline and translation census

This is the preparatory evidence for [P0](https://github.com/erniecohen/dafny/issues/117)
of the [B3 campaign](https://github.com/erniecohen/dafny/issues/116). It adds developer
tooling and records integration obligations; it adds no verification backend and
changes no product source, options, axioms, verdict baselines or default proof costs.

The baseline is Dafny commit `81062e7df6eecd9d73a0671abd83206d13b67893`, product
`4.11.0+fcb2042d.review.846e5da5`. The [dependency lock](dependencies.json) fixes the
public source and package identities. The [inventory](inventory.json) is generated
from actual translator ASTs, not a search for constructor names. The
[semantic audit](semantic-audit.md) distinguishes reviewed source correspondence
from obligations that an adapter must still implement and test.

## Prior work and current scope

The issue threads contain prior implementation and public CI reports. In particular,
[the aggregate subset report](https://github.com/erniecohen/dafny/issues/116#issuecomment-5981214906)
and [the P0 source/model review](https://github.com/erniecohen/dafny/issues/117#issuecomment-5984749063)
are historical evidence, not acceptance of this baseline. The old `codex/b3-backend`
branch is unavailable, and the current base has no B3 backend or B3 integration
documents; the available local history also contains no B3 implementation commits.
The epic was [explicitly parked](https://github.com/erniecohen/dafny/issues/116#issuecomment-6745016929).
This package starts again from the shipped branch and imports none of that implementation.

B3 main still resolves to the requested `b24a2b2d6101df900e67d3bb151c4e85af4cd16d`.
The historical operational successor from PR 13 is recorded for discovery only;
this package does not adopt it or the separate soundness-model PR 12.

## Boogie provenance

The package suffix `dcd726f6` is a hash of a product diff, **not a source commit**.
The complete chain is:

1. `DafnyCore.csproj`, `dotnet-tools.json` and the NuGet source mapping select
   `3.5.5-review.dcd726f6`. `Scripts/fetch-boogie-packages.sh` obtains the 14 release
   packages and checks `Scripts/boogie-packages.sha256`.
2. [The public release](https://github.com/erniecohen/boogie/releases/tag/v3.5.5%2Breview.dcd726f6)
   publishes those packages and `SHA256SUMS`. All 14 downloaded package digests
   match the checked-in sums; the release sum-file digest is in the lock.
3. Every package's `.nuspec` has repository commit
   `677b5f6cfd74207612865adf1ecc08887ae15953`. Its repository URL remains the inherited
   upstream URL `https://github.com/boogie-org/boogie`; resolve this commit through
   **the fork's release tag**, not that URL alone.
4. The release tag resolves to that exact commit in `erniecohen/boogie`.
   [The tag's workflow](https://github.com/erniecohen/boogie/blob/677b5f6cfd74207612865adf1ecc08887ae15953/.github/workflows/review.yml)
   builds/packages from its checkout, checks all 14 packages, and uploads their sums.
   [The public tag run](https://github.com/erniecohen/boogie/actions/runs/43506186375)
   succeeded at that commit.
5. The tag's `.github/review/base` names upstream `v3.5.5` and product commit
   `5a3ac68700a73b56ab822acaf64c54012d7e4885`; its version script hashes their
   modified-file diff to obtain `dcd726f6`. The tag includes later non-product
   commits. This explains both identities without interpreting the suffix as a SHA.

This is a checked published-provenance chain, not an independent reproducible-build
attestation. The package checksums identify artifact bytes; source and version strings
alone do not identify the executing compiler or solver bytes.

## Reproducing the emitted-feature inventory

Build the exact baseline with the checksum-pinned packages first, as in `REVIEW.md`.
Then, with its .NET SDK on `PATH`, run from the repository root:

```sh
python3 .github/review/b3-census.py --work work/b3-p0-census \
  --binaries Binaries/net10.0 --output work/b3-p0-census/inventory.json
```

The runner builds the separate census program beneath `work/`, runs its controls,
and processes [.github/review/b3-corpus.json](../../../.github/review/b3-corpus.json).
It never calls a solver or `GetVerificationTasks`. It parses/resolves the Dafny
sources, lets the existing type/cardinality validation run, calls
`BoogieGenerator.Translate`, then resolves and typechecks each emitted Boogie program.
The prelude row parses the actual prelude with the default Unicode conditional;
arithmetic mode is the default 1. The source-mode rows use the refreshed type system,
datatype traits and general newtypes; library rows also use their reads, determinism
and print-effect options. All other options, including additional axioms and legacy
switches, retain their defaults.

The corpus covers the prelude; standard-library Math, sequences, wrappers and mutable
arrays; and soundness regressions for native map payloads (284), constrained variance
(491), inherited overrides (404), rotations (147), and generalized newtypes (132).
Standard-library dependencies are source inputs for resolution, loaded as unverified
libraries: **the census is not a standard-library verification run**. Its logs retain
those library warnings. Root modules and any additional verifiable abstract signatures
are listed in the inventory, rather than silently dropping translator outputs.

Source hashes normalize CRLF to LF, matching Git text normalization across checkouts.
Counts are sums of distinct AST object identities per translated module. Repeated
preludes across modules count repeatedly; they are not independent axioms. Boogie's
ordinary visitor walks the block representation. A separate control walk records the
preserved structured tree, and includes its guards/invariants. Chained attributes are
visited in full, including subsumption, inline and isolation argument values. Shared expression nodes
count once per module. Declaration attributes, definition-axiom ownership, where
expressions, operator kinds, builtin names, polymorphism, old expressions and control
forms are included. Function calls are grouped by node kind rather than generated name.
This is an empirical census of this configuration, not an exhaustive supported-language
specification or proof of semantic equivalence. Unseen forms remain unsupported until
explicitly handled. The Boogie audit explains why both control representations exist.

## Baseline and bootstrap

The focused Boogie baseline exercises standard-library `Wrappers.dfy`, map-284,
variance-491, and rotation true/false fixtures, using Z3 5.1.0, one core, a 100,000,000
resource-unit limit and a 120-second safety cap. All five commands return their expected
exit status: two success cases and three expected verification failures. There are no
timeout or out-of-resource outcomes. This is focused evidence, not a whole-suite gate
or an independently repeated before/after cost comparison. Resource receipts belong
with a reproduction's execution evidence, not in this source inventory.

Clone the public B3 repository beneath `work/` and check out the exact lock commit;
never copy its sources into the product tree. Set `Z3` to a Z3 5.1.0 executable
and capture its digest/build identity with the execution receipt. Using the baseline compiler:

```sh
dotnet Binaries/net10.0/Dafny.dll build work/b3/src/dfyconfig.toml \
  --output work/b3/bin/b3 --solver-path "$Z3" --cores 3 \
  --resource-limit 100000000 --verification-time-limit 120 \
  --log-format 'csv;LogFileName=work/b3/bootstrap.csv'
```

The strict build verifies the sources but rejects two unchecked-termination warnings:
`DeclarationMarker` in `src/Solver/DeclarationMarkers.dfy` and `Syntax` in
`src/Driver/CommandLineOptions.dfy`, both `{:termination false}`. This was
[escalated on P0](https://github.com/erniecohen/dafny/issues/117#issuecomment-6991848525).
A diagnostic repeat with **`--allow-warnings`** verifies and compiles successfully,
without B3 edits or disabled type/cardinality checks. This establishes a qualified
bootstrap with explicit unchecked termination premises, not a warning-free bootstrap
or an end-to-end soundness theorem. The generated C# project targets `net8.0` with
major runtime roll-forward; the actual SDK/runtime identities are in the lock. Owner acceptance of those premises remains open.

Runtime controls `procedure TrueControl() { assert true }` and
`procedure FalseControl() { assert false }` respectively complete without a proof
error and report `Error: Failed to prove assert false`. `assert 0` is rejected as
ill-typed. **All three return process exit code zero.** The pinned CLI prints errors
and returns without propagating a verification outcome. A later worker must expose
structured non-success results; merely launching this CLI successfully cannot count
as verification. No B3-to-Dafny adapter is implemented here.

## Change manifest and next boundary

This package adds only this record, its lock/inventory, a developer census runner,
a separate census project and controls, and a release-note fragment. No `Source/`
file is changed; no lit test or expected-verdict row is added, and the obligation
producer inventory and `.github/review/base` are unchanged. The census controls
are tooling tests, not new verifier axioms or product checks. To run them and their
serial mutation checks after building the baseline:

```sh
python3 .github/review/b3-census-mutations.py --work work/b3-p0-mutations
(cd .github/review && python3 -m unittest test_b3_census)
```

The standalone census has 21 checks, including rejection of an ill-typed Boogie
program, exclusion of comment text, and recording of chained attributes and
subsumption values. Twelve serial mutations remove where, free
requires, free ensures, structured controls, attributes, operators, hideable axioms,
type checking, old-expression counting, attribute values, builtin names and polymorphism respectively.
Each builds successfully and is rejected by the intended control. The bookkeeping
tests also reject missing cases, root identities, module denominators and required
features, and invalid counts.

Before P1/P2 implementation, resolve warning-policy acceptance and retain the
semantic checklist in the audit as open adapter obligations. Default-Boogie suite/resource
parity, normalized command correspondence, structured worker results, and a proof
connecting B3's executing implementation to a soundness model remain future work.
