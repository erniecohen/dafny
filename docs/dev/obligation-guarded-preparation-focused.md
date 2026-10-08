# Guarded contract preparation: current focused result

Semantic candidate `338b82396`, corrected product pin `69dc750b5`, build source
`861472a9d` and Z3 5.1.0 are the current measured inputs. The native version is
`4.11.0+fcb2042d.review.4bb429b4`. The
[build](https://github.com/erniecohen/dafny/actions/runs/37776297807) passes all ten
actual stages, both native platforms, both probes, the editor build, 92 structural
observations in capture and normal modes, the independent 287-group inventory
and all 370 core unit tests. An earlier test type-name error and a registry
text-decoding mismatch are corrected; neither was a completed candidate gate.

## Only the known regressions and targeted controls

The complete focused diagnostic has 146 observations on the macOS arm64 native
build: the six existing suite regressions, Power, and original issue/subset/new
preparation controls. It preserves the original proof files, project settings,
resource ceilings, solver options and one-core verification commands. The known
regressions use seeds 0, 1 and 7, with both additional-axiom settings and
baseline/default-off/enabled modes. Only four suite source files are involved;
no whole-suite or whole-library run was launched.

All 42 selected baseline/default-off outcome and resource vectors agree exactly.
This is focused compatibility evidence. It does not discharge the preceding
complete-library strict resource rejection or establish complete compatibility.

The following vectors are identical for both additional-axiom settings.
`V` means Correct, `R` means OutOfResource, and `E` means a proof error; positions
are seeds 0, 1 and 7 in that order.

| Target | Baseline/default-off | Enabled |
| --- | --- | --- |
| `ExtensibleArray.Append` | VVV | VVV |
| `SnapTree.Iterator.MoveNext` | VRR | RVV |
| `FormArmy` | VRR | RRR |
| `AltPrimeDefinition` | VVR | VRR |
| `Composite` | VVV | VRR |
| `RemoveFactor` | VRR | RRR |
| `Power.LemmaPowSubtractsAuto` | VVV | EEE |

`ExtensibleArray.Append` verifies across all selected seeds in this focused
platform scope. This is not a like-for-like replay of the previous full Linux
gate, nor attribution of the improvement to the new normalization. Each of the
other five suite targets still fails at least one selected seed. The iterator
has partial success in both baseline and enabled modes and remains the
request-section-13 variance case. The other movements are retained rather than
waived. Power's stable proof failure remains unresolved.

All sixteen positive control observations verify, including the unchanged
original issue 100 and the subset reproducer. All four negative guarded-argument
observations contain genuine Invalid VCs in the body while their independent
specification-WF checks remain Correct, under both resolvers and both axiom
settings. These controls do not make the known-regression diagnostic accepted:
its execution is complete and its targeted enabled acceptance remains false.

## What the Power result establishes

The generated native body confirms that guarded-assumption normalization and
unconditional private argument bindings actually occur. The two real exit
checks remain present at their assertion fuel layers. The native candidate still
fails the second quantified postcondition at every selected seed.

The earlier successful generated-Boogie normalization experiment removed empty
branches as part of flattening. The compiler candidate retains their guard
expressions. That difference, and the fidelity of reparsed Boogie versus native
verification, require controlled investigation before another compiler change.
The successful earlier experiment cannot be reported as a successful native
Power repair. No source proof hints, ceilings or expected verdicts were changed.

The supported-line PR remains a draft. Independent soundness review, remaining
regression work and complete acceptance remain open. Another whole-suite run is
not an iteration step while these known regressions remain unresolved. The
development-line port still requires explicit owner approval.
