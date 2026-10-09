# Native source exit skeleton comparison

The complete pure exit fragment is constructed at its original local point.
This scratch-only diagnostic withholds it before root statement collection,
so native anonymous naming and successor/block creation see the source's
original two terminal returns. The exact pinned Boogie unified-exit pass then
creates the shared exit, where the entire fragment is appended once, in order.
No terminal return or block is reconstructed after CFG generation. Every original
source command, native body command, actual check/full attribute, preparation,
fuel traversal and publication remains. Every actual exit path has one mandatory
postcondition check; no dependency copies or new IDs are introduced.

This tests a remaining distinction from the rejected post-CFG unified-exit and
pre-CFG terminal-copy constructions. Unsupported explicit labels/shapes reject.
Body traversal audits command retention; it never generates support from body
terms. Require faithful native full-input/cost controls and genuine false entries
at unchanged sources/ceilings. This is not an adopted product policy.
