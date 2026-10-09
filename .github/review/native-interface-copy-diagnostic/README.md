# Original call-interface object reuse diagnostic

Construct the locally checked-call publication interface from the already
translated original procedure, retaining requirement/postcondition expression
objects, formal/type/heap bindings, frame expressions and descriptions. Copy
metadata and allocate fresh dependency IDs and distinct dependency entries from
factories captured at original translation. Each factory retains the original
source origin and Dafny expression, without retranslating any Boogie expression.
Require the per-declaration coverage set to gain exactly one distinct entry for
each copied dependency. Earlier scratch evidence reusing the same dependency
objects is diagnostic only: fresh ID strings alone do not prevent entries from
merging in the coverage set. Mark only the original locally checked user
requirements with the existing always_assume attribute; old allocation and inherited/free-call
publication keep their existing behavior. Keep all actual local preparation,
checks, fuel and exit translation unchanged. No source/body/context collection
or additional fact is introduced. This removes unnecessary specification
retranslation, not support. Require faithful native controls, actual formula
and attribute comparisons and genuine negative controls before considering any
product adoption. No normal workflow enables this scratch experiment.
