# Native rotation correspondence diagnostic

This source checkpoint investigates the native verifier before the B3 rotation
route is accepted. It introduces no product change and changes no expected suite
verdict. The fixed public scratch job uses immutable compiler 4.11.0+fcb2042d.review.a171069d and
Z3 5.1.0 inputs with explicit archive and executable hashes; its result is diagnostic
receipt data, including expected discrepancies, rather than workflow success.

For width 3, rotating the word 1 right by 3 and then left by 0 must give 1.
The first rotation moves each bit by a full cycle, and the second moves none.
The fixed input tuple x=1, a=3, b=0 meets the source word/natural/count guards.
The paired postconditions equal to 1 and equal to 4 therefore have different
mathematical truth values. Primitive rotation and same-direction controls remain
separate, and an identical-precondition `assert false` control must fail. The
portable modulo-width shift/or expressions and free-symbol width-3 queries
provide independent correspondence controls; they do not prove equivalence for
every possible source width or every solver implementation path.

The job records every one of 20 fixed sources and all outcomes, two exact native
proof batches per Dafny program, zero seeds, original resource limits, actual
emitted BPL and SMT, and raw solver resource replies. No expectation is replaced
by an observed result. Caller timeouts are safety caps. No arithmetic flags,
seeds, hashes, solver queries or reported proof costs are normalized. The fixture
inventory is checked before execution. The native CLI's actual arithmetic/MBQI
options are inspected after execution. All proof input guards must verify, and
unknown/frontend/tool failures make the diagnostic incomplete.

B3's source rotation correspondence stays pending. A complete reproduction with
an incorrectly accepted postcondition must be tracked separately from backend
implementation, after examining the public receipt. No default backend fix or
upstream message is authorized by this diagnostic checkpoint.
