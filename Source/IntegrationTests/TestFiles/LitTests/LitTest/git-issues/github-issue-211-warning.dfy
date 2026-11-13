// Opt-in --warn-unchecked-termination (https://github.com/erniecohen/dafny/issues/211, item 1):
// "dafny verify" reports each trait with {:termination false} and each class or trait with
// @AssumeCrossModuleTermination, which "dafny audit" reports as unchecked termination assumptions,
// as it reports an assume statement without {:axiom}: a warning, which fails the command unless
// --allow-warnings is given, and which {:axiom} on the declaration, or --allow-axioms, suppresses.
// Only declarations verified in this run are reported: not those of a library (--library,
// --standard-libraries, a .doo file) or of an included file, which the user cannot annotate.
// Off by default: without the option nothing is reported.
//
// RUN: echo "trait, option off: no warning, verifies" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false "%S/Inputs/github-issue-211-warning-trait.dfy" >> "%t"
// RUN: echo "trait, option on: warning, fails without --allow-warnings" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination "%S/Inputs/github-issue-211-warning-trait.dfy" >> "%t"
// RUN: echo "trait, option on, --allow-warnings: warning, verifies" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --allow-warnings "%S/Inputs/github-issue-211-warning-trait.dfy" >> "%t"
// RUN: echo "trait, option on, --allow-axioms: no warning, verifies" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --allow-axioms "%S/Inputs/github-issue-211-warning-trait.dfy" >> "%t"
// RUN: echo "class, option off: no warning, verifies" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false "%S/Inputs/github-issue-211-warning-class.dfy" >> "%t"
// RUN: echo "class, option on: warnings, fails without --allow-warnings" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination "%S/Inputs/github-issue-211-warning-class.dfy" >> "%t"
// RUN: echo "class, option on, --allow-warnings: warnings, verifies" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --allow-warnings "%S/Inputs/github-issue-211-warning-class.dfy" >> "%t"
// RUN: echo "class, option on, --allow-axioms: no warning, verifies" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --allow-axioms "%S/Inputs/github-issue-211-warning-class.dfy" >> "%t"
// RUN: echo "trait, --modular-termination: the unbounded dispatched call is rejected; the warning stays" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --modular-termination "%S/Inputs/github-issue-211-warning-trait.dfy" >> "%t"
// RUN: echo "bounded trait, --modular-termination without the warning option: verifies" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination "%S/Inputs/github-issue-211-warning-bounded.dfy" >> "%t"
// RUN: echo "bounded trait, --modular-termination: warning, fails without --allow-warnings" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --modular-termination "%S/Inputs/github-issue-211-warning-bounded.dfy" >> "%t"
// RUN: echo "bounded trait, --modular-termination, --allow-warnings: warning, verifies" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --modular-termination --allow-warnings "%S/Inputs/github-issue-211-warning-bounded.dfy" >> "%t"
// RUN: echo "class, --modular-termination, --allow-warnings: sealed foreign extension is rejected; the warnings stay" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --modular-termination --allow-warnings "%S/Inputs/github-issue-211-warning-class.dfy" >> "%t"
// RUN: echo "user of Std.Actions, --standard-libraries: no warning for the library's 20 declarations" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --warn-unchecked-termination "%S/Inputs/github-issue-211-warning-std-user.dfy" >> "%t"
// RUN: echo "user of a --library file (--allow-warnings for the CLI warning about a .dfy library): no warning for the library's declarations" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --allow-warnings --library "%S/Inputs/github-issue-211-warning-trait.dfy" "%S/Inputs/github-issue-211-warning-lib-user.dfy" >> "%t"
// RUN: echo "user that includes the file: no warning for the included declarations" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination "%S/Inputs/github-issue-211-warning-include-user.dfy" >> "%t"
// RUN: echo "the same, with --verify-included-files: the included declarations are reported" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --warn-unchecked-termination --allow-warnings --verify-included-files "%S/Inputs/github-issue-211-warning-include-user.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
