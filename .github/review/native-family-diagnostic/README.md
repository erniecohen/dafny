# Native translation-family isolation diagnostic

This scratch-only build isolates five exit-family gates: method return and
fallthrough checking, the corresponding nonchecking method ensures, iterator
fallthrough checking, and the corresponding iterator ensures. Other opt-in
translation gates form the second family. Legacy exit mode retains the checked
procedure contract; legacy call mode retains checked callee requires. No
obligation is dropped merely to isolate the translation families.

The other family also controls implicit obligations within an exit's local WF
preparation, an interaction retained in the diagnosis. Declaration names select
only the implementation to audit and bound the proof run; family gating applies
consistently across the translated module. Native baseline/current controls and
genuine false-entry controls are required. Original source, limits, axiom settings
and reveal scope remain unchanged. This is not a proposed product policy or an
acceptance gate, and normal product workflows do not enable it.
