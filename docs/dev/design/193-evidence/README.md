# Issue 193: reproducible design evidence

This directory retains **design probes and their public measurements**, not an
implementation of the proposed language feature. No compiler or runtime source
is changed by these files.

## Public provenance

- [Main probe run](https://github.com/erniecohen/dafny/actions/runs/42754841657),
  benchmark commit `79d20aa`: 17 operations, four widths, eight modes, both
  resolvers and both solvers. It also retains 100 finite ground model witnesses
  per configuration.
- [Verified sequence proof run](https://github.com/erniecohen/dafny/actions/runs/42755593223),
  benchmark commit `e046a64`: an assumption-free generic sequence/reference
  correspondence, with the additional-axioms option explicitly OFF.
- Both use compiler source
  [`62bae981952337d312cbb3d43c39f9813cca6084`](https://github.com/erniecohen/dafny/tree/62bae981952337d312cbb3d43c39f9813cca6084),
  reporting `4.11.0+fcb2042d.review.2d6fd450`. The compiler artifact came from
  [its public validation run](https://github.com/erniecohen/dafny/actions/runs/42748364832).
  Its `Dafny.dll` SHA-256 is
  `94c07b608c403f08a73fd4fa210ac79bcf2c8426df4ec436b274a3c7496958c4`.

The three Python scripts in this directory are byte-for-byte copies of the
authoritative files at benchmark commit `79d20aa`. `sequence-model.dfy` is the
exact public proof input, renamed from `sequence_model.dfy` without changing its
bytes. Its SHA-256 is
`e5eeaa8c83e55b65de9374a2ca9990d3faa48ad5b1793f31df8f081a4b950f43`.

The solvers are the checksum-pinned distributions used by the repository's
public workflow:

| Solver | Distribution | Archive SHA-256 | Executable SHA-256 |
| --- | --- | --- | --- |
| Z3 5.1.0 | [Z3 release, Linux x64 glibc 2.39](https://github.com/Z3Prover/z3/releases/download/z3-5.1.0/z3-5.1.0-x64-glibc-2.39.zip) | `f47be8d27d3230e823bf1eeede2fe0abaca55bb78d0b59974370e6689a92284a` | `b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23` |
| Z3 4.12.1 | [Dafny solver snapshot](https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-4.12.1-x64-ubuntu-24.04-bin.zip) | `e07d4193d8e1851114fe4d507332a43576639f4aeaa33c7df15c36426d3cbf3b` | `3b8b24b2130bfcc88a3b3bca9617c9a8721e90573ead7af9dbb371a2bf62aa43` |

## Reproduce the main measurements

Use the pinned compiler and a compatible .NET runtime. Set `DAFNY193_DLL`,
`DOTNET193`, and `Z3_193` to the respective absolute executable/file paths. A
source rebuild should use the pinned source and its normal build workflow,
including checksum-verified Boogie packages. Record new binary hashes; do not
silently identify a different rebuild or solver distribution as the binaries
measured here.

From this directory:

```sh
python3 generate.py probe-corpus
python3 run.py \
  --dafny "$DAFNY193_DLL" --dotnet "$DOTNET193" --z3 "$Z3_193" \
  --corpus probe-corpus --output probe-results \
  --source-commit 62bae981952337d312cbb3d43c39f9813cca6084 \
  --resource-limit 200000 --workers 2 --solver-log
python3 ground_controls.py --z3 "$Z3_193" --output ground-results
```

Repeat for both pinned solvers. Add `--refresh` to `run.py` for the refreshed
resolver and use a different output directory. For a self-contained Dafny
executable, omit `--dotnet` and pass that executable as `--dafny`.

The runner selects `--additional-axioms`, one prover core per invocation,
`/normalizeDeclarationOrder:0`, `/restartProver`, a 200,000-RU cap and a 15-second
safety timeout per batch. It records expected failures and exits successfully:
**workflow success is delivery of measurements, not acceptance of the feature.**
Use the recorded invocation exits and per-scope/per-VC outcomes.

The current source expressions use natural shift counts directly. The earlier
explicit-cast exploratory variant is not the source of these tables.

## Meaning of each mode

| Mode | What it measures |
| --- | --- |
| `assume_false` | Reconstructed per-operation vacuous baseline, based on the issue description. It is not the downstream library's unavailable original source. |
| `native` | The current compiler's empty-body proof attempt for the same integer-meaning postcondition. Failure is not evidence that the mathematical target is false. |
| `candidate` | A trusted bridge contract over `bvN` operands, called with casts of bounded natural arguments. |
| `vacuity` | Concrete call to each raw bridge followed by `assert false`, with transparent recursive definitions present. Resource exhaustion is inconclusive. |
| `fused` | A trusted bridge contract over the original bounded natural arguments, combining the operation fact and bounded conversion round trips. |
| `fused_vacuity` | The corresponding full-context concrete false control for the fused contract. |
| `native_helpers` | Assumption-free native-bitvector proofs of OR, XOR, and andNot reductions. |
| `int_reference` | Assumption-free definitional checks of pure integer reference wrappers. |

The raw and fused APIs explicitly trust a contract per operation. They are
broader than the design's proposed minimal kernel. These measurements do not
establish the cost of deriving all operations from that kernel, nor do they
measure the production local-fact emitter. The arithmetic reference wrappers
describe bitvector modulo arithmetic, not new semantics for ordinary integer
newtype addition, subtraction, or multiplication.

The finite ground controls evaluate the recursive integer references at concrete
arguments and check satisfiability of the resulting native operation fact plus
the negation of `assert false`. Rotations use shifts and OR. These witnesses
complement the full Dafny contexts; they do not establish consistency of the
entire prelude or replace an inconclusive full-context vacuity control.

## Reproduce the verified sequence correspondence

Restore the original input basename when comparing resource counts:

```sh
mkdir -p sequence-results
cp sequence-model.dfy sequence-results/sequence_model.dfy
"$DOTNET193" "$DAFNY193_DLL" verify sequence-results/sequence_model.dfy \
  --additional-axioms=false --cores 1 --verification-time-limit 15 \
  --resource-limit 2000000 --solver-path "$Z3_193" \
  --boogie /normalizeDeclarationOrder:0 --boogie /restartProver \
  --log-format 'json;LogFileName=sequence-results/sequence-model.json' \
  --log-format 'csv;LogFileName=sequence-results/sequence-model.csv' \
  --solver-log sequence-results/sequence-model.smt2 \
  --type-system-refresh:false --show-snippets:false --use-basename-for-filename
```

Repeat with `--type-system-refresh:true` and both solvers, retaining separate
output directories. Unlike the trusted API projections, this file contains no
assumptions, axiom contracts, bodyless declarations, or disabled verification.
It proves a generic correspondence between four pointwise sequence operations
and the recursive natural references, for arbitrary natural widths and operands.
The public run gives 17 verified and 0 errors in every configuration.

It does not yet prove the full bits/value bijection, arithmetic/rotation library,
or native bridge. The production work still needs those proofs.

## Retained observations

- `public-measurements.md`: compact tables, exact interpretations and outstanding
  vacuity failures for both public runs.
- `public-client-batches.csv`: all 3,312 observed client/control scopes.
- `public-all-batches.csv`: all 5,088 observed scopes/VCs, including bridge and
  reference-definition well-formedness.
- `public-files.csv`: exits and source hashes for all 128 main invocations.
- `public-ground-controls.csv`: all 400 ground model-witness observations.
- `public-receipts.json` and `public-resolver-comparison.json`: binary identities,
  options and exact resolver comparison.
- `sequence-batches.csv`: all 68 scope/VC results of the generic sequence proof.
- `sequence-receipts.json`: its exits, source/compiler/solver hashes, options and
  resolver comparison.
- `check-runtime.cjs` and `javascript-diagnostic.txt`: the exact public JavaScript
  diagnostic source and recorded output for the existing runtime defect now
  tracked in [issue 246](https://github.com/erniecohen/dafny/issues/246).

The summary table's correctness costs must not be mistaken for whole-file
success; the full batch data retain all well-formedness checks. JSON outcomes
distinguish `Errors` from `OutOfResource`, which Dafny's CSV logger alone does
not. A resource count above the nominal cap does not by itself change the
recorded outcome.

No option-off identity or option-on suite/standard-library performance delta for
the proposed production feature follows from these probes. The required
implementation gates remain separate.
