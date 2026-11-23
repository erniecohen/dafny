// Extern linkage within a group still needs a foreign-package convention.
// NONUNIFORM: Go group linkage admission.
// RUN: %exits-with 3 %build --no-verify --target go --member-invariants --recursive-modules "%s" --output "%t" > "%t"
// RUN: %exits-with 3 %build --no-verify --target go --member-invariants --recursive-modules "%S/Inputs/git-issue-466-go-extern-type.dfy" --output "%t-type" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target go --member-invariants --recursive-modules "%S/Inputs/git-issue-466-go-extern-member.dfy" --output "%t-member" >> "%t"
// RUN: %diff "%s.expect" "%t"
module {:extern} A { import B function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
