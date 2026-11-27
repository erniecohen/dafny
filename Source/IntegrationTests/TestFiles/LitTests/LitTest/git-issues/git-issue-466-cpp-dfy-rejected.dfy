// Neither backend can qualify the runtime matrix; reject source and archive groups.
// NONUNIFORM: Admission and independent capability controls for C++ and resolved Dafny.
// RUN: %exits-with 3 %translate cpp %trargs --solver-path %review-z3 --no-verify --unicode-char:false --member-invariants --recursive-modules "%S/Inputs/git-issue-466-cpp-dfy-functions.dfy" --output "%t-functions.cpp" > "%t"
// RUN: %exits-with 3 %translate cpp %trargs --solver-path %review-z3 --no-verify --unicode-char:false --member-invariants --recursive-modules "%S/Inputs/git-issue-466-cpp-dfy-mutual.dfy" --output "%t-mutual.cpp" >> "%t"
// RUN: %exits-with 3 %translate dfy %trargs --solver-path %review-z3 --no-verify --member-invariants --recursive-modules "%S/Inputs/git-issue-466-cpp-dfy-functions.dfy" --output "%t-functions.dfy" >> "%t"
// RUN: %build --cores 2 --solver-path %review-z3 -t:lib --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage7-library.dfy" --output "%t.doo" > "%t-reference"
// RUN: %run --cores 2 --solver-path %review-z3 --target cs --member-invariants --recursive-modules "%S/Inputs/git-issue-466-java-client.dfy" --input "%t.doo" --output "%t-cs" >> "%t-reference"
// RUN: %diff "%S/Inputs/git-issue-466-cpp-dfy-reference.expect" "%t-reference"
// RUN: %exits-with 3 %build --cores 2 --solver-path %review-z3 --no-verify --target cpp --member-invariants --recursive-modules "%S/Inputs/git-issue-466-java-client.dfy" "%t.doo" --output "%t-library.cpp" >> "%t"
// RUN: %exits-with 3 %build --cores 2 --solver-path %review-z3 --no-verify --target dfy --member-invariants --recursive-modules "%S/Inputs/git-issue-466-java-client.dfy" "%t.doo" --output "%t-library.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// These controls have no recursive group: their failures are backend limitations.
// RUN: %exits-with 3 %translate cpp %trargs --solver-path %review-z3 --no-verify --unicode-char:false --general-traits=legacy "%S/Inputs/git-issue-466-cpp-dfy-trait.dfy" --output "%t-trait.cpp" > "%t-trait"
// RUN: %OutputCheck --file-to-check "%t-trait" "%S/Inputs/git-issue-466-cpp-dfy-trait.check"
// RUN: %exits-with 3 %translate dfy %trargs --solver-path %review-z3 --no-verify "%S/Inputs/git-issue-466-cpp-dfy-acyclic.dfy" --output "%t-ordinary.dfy" > "%t-ordinary"
// RUN: %OutputCheck --file-to-check "%t-ordinary" "%S/Inputs/git-issue-466-cpp-dfy-printer.check"
