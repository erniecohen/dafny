# Scoped diagnostic repair for issue 48

This change replaces the refreshed resolver's placeholder `bad` messages for failed proxy subtype constraints. It uses the enclosing diagnostic and names the covariant or contravariant parameter and the failing subtype relation. `{0} :> {1}` denotes the stored constraint's supertype and subtype, matching the existing variance-constraint diagnostics.

The constraint operands, insertion order, proxy creation, and error-reporting flags are unchanged. The legacy resolver is untouched. The original inference issue remains open: implicit literals in the supplied conditional/match program are still rejected by the refreshed resolver. Explicit casts still resolve in both modes.

## Focused evidence

A safe scratch probe built the shipped baseline and the candidate and recorded eight resolution commands for each. Both builds succeeded. All eight exit statuses and error counts were preserved:

| Case | Legacy | Refreshed |
| --- | --- | --- |
| Original conditional/match newtype reproducer | accepted | one type error |
| Explicit casts of both literals | accepted | accepted |
| Incompatible boolean datatype argument | one type error | one type error |
| Incompatible function input types | two type errors | two type errors |

The original refreshed error now includes `Function body type mismatch`, `Option<int32>`, and the failed covariant relation `int32 :> int`. Both former placeholder errors in the function-input case now name contravariant parameter `T0`; they exercise the two orientations of the generated subtype constraint. All other focused diagnostics are unchanged.

The probe is recorded at https://github.com/erniecohen/dafny/actions/runs/37266982779. This message repair makes no change to inference or accepted programs.
