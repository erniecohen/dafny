// RUN: python3 "%S/Inputs/github-issue-211-marked.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" coverage > "%t"
// RUN: %diff "%s.expect" "%t"

// Soundness gate (b) of the plan for https://github.com/erniecohen/dafny/issues/211: every way code runs
// is ordered by the rules of --modular-termination.
//
// Each input Inputs/github-issue-211-gate-b-*.dfy has a library module with an open trait, and a client
// module whose marked members each evaluate a dispatched member of the trait in one context without the
// smaller bound that the rules need.  If a context escaped the rules, an implementation of the trait that
// calls the marked member would recurse without end, and with a postcondition would prove false.  The
// driver runs every input under both resolvers and fails if a marked member gets no error on its line.
//
// Where each kind of evaluation is ordered:
//  - calls in expressions and statements, specification clauses, and the bodies of by-methods
//    (expressions, statements, by-method): the verifier, at each call
//    (CheckModularTermination, from CheckWellformed of a function call and from TrCallStmt);
//  - least and greatest predicates and lemmas and their prefix forms, constructors, and iterators
//    (declarations): the same checks; prefix predicates and lemmas stand for their extreme declarations
//    in the call graph of the option (ModularTerminationAnalysis.NodeOf);
//  - default parameter values (defaults): the verifier checks the value where the call is made, as part
//    of the caller;
//  - lambdas (lambdas): the verifier checks the body under the bound of the code that creates it, or
//    under the 'decreases {:callback}' bound of the parameter it is passed for;
//  - function values that are not lambdas (values, functions): the resolver refuses a call of a value
//    whose code is not known when the caller has a bound (CallClassifier in ModularTermination.cs);
//  - constant initializers, subset-type constraints and witnesses, and default values of datatype
//    components (types, types-refresh): the resolver refuses a use that may evaluate one that reaches
//    open dispatch when the user has a bound (CallClassifier, with TypeMentions, in ModularTermination.cs).
// The input 'accepted' is the control: the same contexts within the rules, with 'assert false' controls.
