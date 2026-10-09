# Certified private argument-binding diagnostic

The scratch diagnostic substitutes defined values for fresh function-WF argument
aliases only inside a newly generated, accepted certified preparation fragment.
For a fresh private t and pure e, eliminate t := e and transfer subsequent Q(t)
to Q(e) in original command order. Source state is unchanged and facts over
observable values are equivalent. Original guards, function symbols, boxing,
heaps and fuel are preserved in the value-substituted support.

Only ground fragments containing comments, assumptions and unique simple marked
argument assignments are admitted. Reject havoc, source writes, repeated/forward
bindings, binders, old and unsupported expression kinds. Clone rewritten trees.
Audit that every eliminated argument has no surviving expression use or where
clause in the translated target. Actual check expressions and attributes are
compared with strict native-control fingerprints; negative controls remain
genuinely Invalid. Selected names bound the diagnostic, not a product policy.
Normal workflows do not enable this experiment and it supplies no acceptance.
