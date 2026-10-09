# Original call-interface object reuse diagnostic

Construct the locally checked-call publication interface from the already
translated original procedure, retaining requirement/postcondition expression
objects, formal/type/heap bindings, frame expressions and descriptions. Copy
metadata and allocate fresh dependency IDs that reference the same source
dependencies. Mark only the original locally checked user requirements with the
existing always_assume attribute; old allocation and inherited/free-call
publication keep their existing behavior. Keep all actual local preparation,
checks, fuel and exit translation unchanged. No source/body/context collection
or additional fact is introduced. This removes unnecessary specification
retranslation, not support. Require faithful native controls, actual formula
and attribute comparisons and genuine negative controls before considering any
product adoption. No normal workflow enables this scratch experiment.
