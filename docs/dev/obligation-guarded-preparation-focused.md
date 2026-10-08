# Guarded contract preparation: current focused result

**Newest correction:** semantic/product pin `a709df226` replaces empty
certified-WF branches by `G ==> G`, retaining the guard terms without an empty
control-flow split. Its [build](https://github.com/erniecohen/dafny/actions/runs/37781426875)
passes all ten actual stages, both platforms and probes, the editor build,
92 structural observations in each mode, the exact 287-group inventory and all
370 core unit tests. The 68-observation native known-regression diagnostic is
pending; no preceding result validates this edit. Whole-suite runs are not an
iteration step while the known regressions remain unresolved.

The preceding measured semantic candidate `338b82396`, corrected product pin
`69dc750b5`, build source
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
and `AltPrimeDefinition` have partial success in both baseline and enabled modes
in this current matrix, so request section 13 classifies them as solver/resource
variance rather than translation defects. The remaining movements stay open.
Power's stable proof failure remains unresolved.

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


## Follow-up empty-branch diagnostic

Thirty focused generated-Boogie observations replay the current compiled Power
body on the same platform with Z3 5.1.0, both axiom settings and seeds 0, 1 and 7.
The unchanged reparsed native control fails each seed. Removing the one empty
certified-WF branch, or replacing it by `G ==> G` with its original guard,
verifies every seed. All false exit controls fail, and both real exit checks are
byte unchanged in the positive variants. The baseline controls verify. The
static SMT prefix is identical, including background declarations, axioms and
solver options; the dynamic verification condition changes with the branch.

The proposed compiler correction uses the tautology, preserving guard terms
without an empty control-flow split. Its fresh build and native result remain
pending. These replay outcomes identify a cause in generated Boogie; they do not
establish native candidate acceptance or repair the remaining resource cases.
