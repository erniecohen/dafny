# Remaining resource cases: immediate-assertion diagnosis

The current semantic/product `a709df226` has a completed focused native Power
repair, but three non-exempt suite targets retain resource failures. A separate
42-observation diagnostic compares their original sources with copies that add
an immediate explicit assertion of each final postcondition. It uses the exact
baseline `ab210b78b` native build, Z3 5.1.0, unchanged original flags and ceilings,
macOS arm64, both axiom settings and seeds 0, 1 and 7. Original-control source
bytes are unchanged. The preceding current-enabled results are explicitly
reused from the completed 68-observation native result, not re-executed here.
No diagnostic assertion is a proposed product proof hint.

Both axiom settings give the same vectors. `V` means Correct and `R` means
OutOfResource; positions are seeds 0, 1 and 7.

| Target | Original baseline | Baseline plus immediate assertions | Current enabled, reused |
| --- | --- | --- | --- |
| `FormArmy` | VRR | RRR | RRR |
| `RemoveFactor` | VRR | RVR | RRR |
| `Composite` | VVV | VRV | VRR |

All six false-entry controls contain genuine Invalid body VCs. Every reported
independent specification-WF result is Correct; trivial specifications may emit
no separate WF result row. The initial harness incorrectly required such a row
and stopped; the preserved incomplete attempt is not counted as a completed
comparison. A fresh unchanged-input copy corrects only that reporting rule and
completes all 42 observations.

## What this does and does not explain

Ordinary explicit assertion machinery also exhausts the original resource
ceiling in these real examples. `FormArmy` reproduces the enabled all-failing
vector. The other two have assertion seeds that verify where enabled mode
exhausts the limit, so this is not a blanket explanation or compatibility waiver.
The remaining resource differences require further generated-Boogie diagnosis.

`RemoveFactor`'s immediate explicit exit check and the enabled exit check have
the same product applications, operands and two fuel layers. The difference is
not a smaller fuel allowance in the enabled exit proposition. Its leading
can-call support is repeated before and after certified preparation in enabled
mode; the explicit source assertion places support after its ordinary WF
traversal. Whether that order affects the resource result is not established.

Adding a source assertion also changes which original statement is terminal.
In `RemoveFactor`, the diagnostic source copy has scope push/pop markers around
calculation hints that the original terminal-body translation retains without
those markers. Therefore this source experiment is not an exact replay at the
original terminal reveal scope. That scope difference must be kept explicit;
it is not evidence that the product should discard previously retained reveals.

`Composite`'s exit preparation/publication is interleaved across conjuncts, while
the added explicit conjunction assertion prepares and checks its split pieces
before whole-assertion publication. Its final prime-condition pieces have the
same quantified trigger terms in the compared generated bodies. Statement
ordering, publication and other strengthened body checks still differ.

No whole-suite or whole-library job was launched. No source ceiling, expected
verdict or product proof hint changed. Targeted enabled acceptance and strict
default-off library resource compatibility remain open. Iterator and
`AltPrimeDefinition` remain the separate section-13 partially successful cases;
this diagnostic does not authorize product tuning for their seed movements.

## Local support-order replay and its limits

A completed 36-observation generated-Boogie diagnostic moves one existing
leading can-call assumption past pure private-argument WF preparation, before
the first actual check. It preserves the original commands, facts, check formulas,
fuel and scope. It includes unchanged controls and false-entry controls for
`RemoveFactor` and `Composite`, both axiom settings and seeds 0, 1 and 7. All
false-entry controls fail genuinely.

The unchanged `RemoveFactor` replay agrees with the preceding native outcomes
and resource vectors; moving the assumption does not repair it. The unchanged
`Composite` replay does not reproduce native verification. Its apparent
improvement after reordering therefore cannot support a native product change.
No compiler change is made from this replay.

The background declarations and axioms are identical in the compared traces,
but the replay is not a faithful serialization of the native verifier state.
Its model-generation setting remains different: error-trace output does not
enable model generation. The generated body also differs after printing and
reparsing. Dafny source conditionals carry `BlockRewriter.AllowSplitQ`; Boogie's
`IfCmd.Emit` does not emit that attribute, although structured-block resolution
copies it to the generated goto. Conjunction grouping and literal-negation
representation can also change. The compared pruned bodies show different dead
paths and SSA variables. These differences explain why printed-program equality
is insufficient for this diagnostic; they do not establish the cause of the
native regression.

The next diagnosis must operate on the native translated AST and require its
unchanged control to reproduce the current native query and result before
attributing any change. It remains limited to the existing failing methods.

The subsequent [native AST comparison](obligation-support-order-diagnosis.md) completes all 36 observations with faithful controls. It improves one Composite seed; remaining resource failures and product acceptance stay open.
