// Foreign declarations in a Python group need a separate linkage convention.
// NONUNIFORM: Python group linkage admission.
// RUN: %exits-with 3 %build --no-verify --target py --member-invariants --recursive-modules "%s" --output "%t" > "%t"
// RUN: %exits-with 3 %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-extern-type.dfy" --output "%t-type" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-extern-member.dfy" --output "%t-member" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-omitted-module.dfy" --output "%t-omitted-module" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-omitted-type.dfy" --output "%t-omitted-type" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-omitted-member.dfy" --output "%t-omitted-member" >> "%t"
// RUN: %diff "%s.expect" "%t"
module {:extern} A { import B function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
