# Native split caller publication comparison

Legacy checked procedure requirements publish checked pieces. They skip free-only
splitter pieces and do not separately assume the entire source precondition.
Current local caller checks publish their ordinary checked pieces and also emit
a split-summary assumption. Isolate that extra summary for the selected caller.
Retain all pre-check WF/can-call support, actual checks, fuel, call position and
nonchecking procedure copy. Generate the existing summary/dependency id before
omitting only its emission, allowing strict actual-check attribute comparisons.
Require faithful native controls and genuine Invalid negatives. No normal
workflow enables this diagnostic. This concerns normal call publication, not a
proposal to reduce assertion-equivalent pre-check support.
