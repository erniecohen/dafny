# Contract checking consolidation experiment

This candidate retains legacy return-position reveal visibility and every original
postcondition formula, inherited guard, trigger and fuel term. It checks the
original implementation postconditions locally at every explicit return and
fallthrough, together with the assertion-style pieces, before publishing each
clause summary. Only the implementation procedure's corresponding user-defined
ensures become free: the callable procedure's contracts and frame/heap boilerplate
are unchanged. Missing saved original checks fail closed. Filtered translation
keeps the previous path.

The local argument is a proof cut: all exits check the exact saved original
formulas, and all added pieces, with ordinary publication, before any summary of
that clause. A summary cannot justify its own original contract. Earlier checked
clauses may assist later clauses in their established order. No hidden definition,
permission, fuel or source contract is removed. Local contract checks force
checking after retaining the inherited-condition guard, so an inherited origin or an assume-mode source block cannot turn a
relocated original check into an unchecked premise. An original ground formula
already checked by an identical canonical piece at that state is reused once;
the conservative comparison retains exact types, names, function/operator forms,
old-state forms, fuel and boxing, and refuses quantifiers, lets and unsupported
syntax. No check is deduplicated across an intervening obligation or state change.
Local call-site checks use ordinary
publication, preserving the method-requires policy; explicit split source assertions
keep their original check-and-forget behavior. Every new and original call
precondition still has to be checked before the actual call.

This is an unaccepted product-repair experiment. Structural and native evidence,
including unchanged complete files and matched seeds, must establish its actual
behavior before any completeness or performance claim. AI assisted the work.
