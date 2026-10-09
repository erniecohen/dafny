# Native unified-exit construction comparison

This scratch-only diagnostic restores the two terminal returns in RemoveFactor,
then calls the exact pinned Boogie `DesugarReturns.GenerateUnifiedExit` pass.
It appends the original certified exit fragment unchanged at that generated exit.
All original commands, fuel traversal, checks, full dependency attributes and
normal publication remain once; there is one mandatory check on each reachable
exit path. No formula, preparation fact, resource ceiling or source proof hint is
changed. Only the terminal control-flow construction is compared. The selected
postcondition still has exactly one static assertion.

The normal control applies no transformation. A false-entry variant must genuinely
fail. Compare the complete native-control solver inputs against the retained
reference before drawing conclusions. The preceding separate-terminal-path
experiment is not a reconstruction of legacy behavior: pinned Boogie already
unifies returns. This comparison uses its actual unified-exit constructor, without
reintroducing a checked procedure postcondition or adding duplicate obligations.
This is diagnostic, not a product policy or a regression repair claim.
