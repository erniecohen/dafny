// Preserve language ownership boundaries before native compilation where possible.
// NONUNIFORM: Rejection controls for Rust refinement and Go foreign packages.
// RUN: %exits-with 3 %translate rs %trargs --no-verify --enforce-determinism --cores 2 --solver-path %review-z3 --member-invariants --recursive-modules --allow-external-contracts --general-traits=legacy "%S/Inputs/git-issue-466-rust-refinement-chain.dfy" --output "%t-rust" > "%t-rust.out"
// RUN: %OutputCheck --file-to-check "%t-rust.out" "%S/Inputs/git-issue-466-rust-refinement-chain.check"
// RUN: %exits-with 3 %translate go %trargs --no-verify --cores 2 --solver-path %review-z3 --member-invariants --recursive-modules --allow-external-contracts --general-traits=legacy "%S/Inputs/git-issue-466-go-foreign-partial.dfy" --output "%t-foreign-partial" > "%t-foreign-partial.out"
// RUN: %OutputCheck --file-to-check "%t-foreign-partial.out" "%S/Inputs/git-issue-466-go-foreign-partial.check"
// RUN: %exits-with 3 %translate go %trargs --no-verify --cores 2 --solver-path %review-z3 --member-invariants --recursive-modules --allow-external-contracts --general-traits=legacy "%S/Inputs/git-issue-466-go-qualified-partial.dfy" --output "%t-qualified-partial" > "%t-qualified-partial.out"
// RUN: %OutputCheck --file-to-check "%t-qualified-partial.out" "%S/Inputs/git-issue-466-go-foreign-partial.check"
// RUN: %exits-with 3 %build --target go --no-verify --cores 2 --solver-path %review-z3 --member-invariants --recursive-modules --allow-external-contracts --general-traits=legacy "%S/Inputs/git-issue-466-go-foreign-cycle.dfy" "%S/Inputs/git-issue-466-go-foreign-cycle.go" --output "%t-cycle" &> "%t-cycle.out"
// RUN: %OutputCheck --file-to-check "%t-cycle.out" "%S/Inputs/git-issue-466-go-foreign-cycle.check"
