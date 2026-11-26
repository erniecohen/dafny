// NONUNIFORM: Rust recursive-group admission controls.
// RUN: %exits-with 3 %build --no-verify --target rs --enforce-determinism --general-traits=legacy --member-invariants --recursive-modules "%s" --output "%t" > "%t"
// RUN: %exits-with 3 %build --no-verify --target rs --enforce-determinism --general-traits=legacy --member-invariants --recursive-modules "%S/Inputs/git-issue-466-rust-extern-type.dfy" --output "%t-type" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target rs --enforce-determinism --general-traits=legacy --member-invariants --recursive-modules "%S/Inputs/git-issue-466-rust-extern-member.dfy" --output "%t-member" >> "%t"
// RUN: %exits-with 3 %build --no-verify --target rs --enforce-determinism --general-traits=legacy --member-invariants --recursive-modules "%S/Inputs/git-issue-466-java-source.dfy" --output "%t-default" >> "%t"
// RUN: %exits-with 3 %translate rs %trargs --no-verify --enforce-determinism --general-traits=legacy --member-invariants --recursive-modules "%S/Inputs/git-issue-466-rust-extern-refinement.dfy" --output "%t-refinement" >> "%t"
// RUN: %diff "%s.expect" "%t"
module {:extern "Foreign", "library"} A { import B function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
