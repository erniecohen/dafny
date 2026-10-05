# Statement-scope allocation regression rows

Both new programs use only existing-language constructs and are registered with the upstream harness. The first RUN uses the legacy resolver with additional axioms disabled. Each file also checks the refreshed resolver and both additional-axiom settings against its recorded oracle.

| Program | First-RUN result | Reason |
| --- | --- | --- |
| `github-issue-132-statement-binders-positive.dfy` | 0 / 7 verified, 0 errors | A proof-local variable or heap label is not a free lambda capture. Actual allocation and evaluation remain provable for declarations, declaration patterns, match cases, binding guards and local labels; a real outer-label positive also remains provable. |
| `github-issue-132-statement-binders-negative.dfy` | 4 / 0 verified, 3 errors | Outer references and outer heaps remain captures. An earlier heap cannot acquire a later proof-captured object or the captured heap after a new allocation, and the inhabited false contract remains rejected. |

These are new test rows, not changes to existing golden verdicts. [Focused CI](https://github.com/erniecohen/dafny/actions/runs/37263041900) records the same intended outcomes for all four combinations. Original allocation regression outcomes remain unchanged with the scope repair. The complete rebased PR must pass its exact-head scratch gate before publication or merge.
