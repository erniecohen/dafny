// Reject foreign forms whose group linkage has not been qualified.
// NONUNIFORM: JavaScript group linkage admission.
// RUN: %exits-with 3 %translate js %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-js-extern-type.dfy" --output "%t-type" > "%t"
// RUN: %exits-with 3 %translate js %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-js-extern-member.dfy" --output "%t-member" >> "%t"
// RUN: %exits-with 3 %translate js %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-js-extern-qualified-type.dfy" --output "%t-qualified" >> "%t"
// RUN: %exits-with 3 %translate js %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-js-extern-refinement.dfy" --output "%t-refinement" >> "%t"
// RUN: %exits-with 3 %translate js %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-js-extern-qualified-method.dfy" --output "%t-method" >> "%t"
// RUN: %exits-with 3 %translate js %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-js-extern-qualified-field.dfy" --output "%t-field" >> "%t"
// RUN: %exits-with 3 %translate js %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-js-extern-constructor.dfy" --output "%t-constructor" >> "%t"
// RUN: %diff "%s.expect" "%t"
