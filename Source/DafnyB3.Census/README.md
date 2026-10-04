# Typed pre-VC census

This nonpackable diagnostic project implements the emitted-IR portion of [P0 #117](https://github.com/erniecohen/dafny/issues/117). It parses and resolves the committed corpus, preserves cardinality admission, invokes the shared translator, and resolves/typechecks the generated Boogie program. It never creates an execution engine, verification task, VC generator, or prover.

From the repository root, run:

```sh
Scripts/run-b3-census.sh
```

The runner fetches checksum-pinned Boogie packages, builds the diagnostic project and writes `docs/dev/b3/emitted-features.json`. Pass a different output path as its first argument. The normal build references the repository's DafnyCore project. For a faster diagnostic build, `DAFNY_CENSUS_REFERENCE_DIRECTORY` may name an extracted exact-source Dafny release directory; `DAFNY_CENSUS_DEPENDENCY_REVISION` must identify that release's source. The report records the loaded assembly version and SHA256. Set `DAFNY_CENSUS_SOURCE_REVISION` and `DAFNY_CENSUS_SOURCE_DIRTY` when running a source snapshot without Git metadata. The prelude always comes from the selected repository's `Source/DafnyCore/DafnyPrelude.bpl`, and its SHA256 is recorded.

`corpus.json` records every input, category, relevant resolver options, expected preparation stage, and source-verification intent. Invalid assertions/contracts still reach the census: this tool does not verify them. The deliberate malformed source and rejected expansive-cardinality input are retained with their diagnostics and distinct rejection stages. An unexpected stage, missing input or exception makes the run fail after writing the complete report.

The census counts unique reachable object identities per translated module, including the real prelude. Shared structured/CFG nodes are counted once. The walker inspects instance fields and materialized collection elements, including private/backing fields, rather than discovering children by arbitrary property getters. Tokens and numbered metadata are omitted explicitly; foreign object kinds are recorded rather than discarded silently. Immutable numeric payloads are classified as literal kinds, while their private implementation fields are not traversed. A small source-reviewed accessor whitelist supplies list counts and names. The internal cardinality receipt is observed through its two known auto-property backing fields; a layout change fails the case. No desugaring, passification, visibility analysis or preprocessing runs during collection.

Per-unit static records list implementation identities, structured/CFG presence, direct assertion descriptions, entry/exit contract counts and call-site precondition/output/frame counts. These are syntax census counts: loop assertions can later produce multiple obligations, and no claim is made that these counts equal the number of proof tasks or executed checks.

Keep input files and expected stages unchanged during an admitted run. When the translator or dependency changes, regenerate the inventory and review new constructs/attributes, omissions, unit identities and diagnostic stages alongside [the semantic correspondence audit](../../docs/dev/b3/semantic-correspondence.md).

## Recorded snapshot

The committed inventory covers 25 input files: 23 typed pre-VC cases, one deliberate parse rejection and one expansive-cardinality resolution rejection. The translated cases contain 57 modules, 72 implementation units, 252 direct assertion commands, 27 checked exit clauses and 10 call sites. The aggregate includes 240 where predicates, 798 residual type parameters and 20,596 resolved type applications. These are deterministic structural counts, not proof or performance measurements.

Dependencies came from [successful exact-source CI run 37157932239](https://github.com/erniecohen/dafny/actions/runs/37157932239), source `8b9635e3d48b94c1a5875f0f2e0a772d9131900b`, compiler build `4.11.0+fcb2042d.review.a171069d`. Its public archive SHA256 was `ba06f4d5048ecf0cc40f79281230eb44b429fd73a8bbed5c4377a3d0ff010331`. The checked checkout additionally contained planning commit `b07c038737d6713b6d1a5848d7568bdc972de7dd`; its `.github/review/base` named last product identifier `e22f5dc03396b58910e23ca49b586d8efee6bd27`. The report records dirty-source status plus collector/input/prelude hashes, since that checkout commit alone does not identify the newly added collector. Its loaded DafnyCore file version was `4.11.0.0`; the actual loaded DLL SHA256 is recorded separately.

The diagnostic project compiled with zero warnings and zero errors. Every preparation-stage expectation matched. No source-verification outcomes were measured by this package.
