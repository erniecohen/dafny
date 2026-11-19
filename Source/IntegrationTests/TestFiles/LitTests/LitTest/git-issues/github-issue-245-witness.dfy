// User well-foundedness witnesses in modular-termination bounds (issue 245).
// RUN: echo "user witness: local/imported global and callback bounds" > "%t"
// RUN: %exits-with 0 %baredafny verify --modular-termination --decreases-orders --type-system-refresh --general-traits=datatype --standard-libraries:false --warn-unchecked-termination=false --show-snippets:false --use-basename-for-filename --solver-path %review-z3 --cores 1 --verification-time-limit 30 --resource-limit 16000000 "%S/Inputs/github-issue-245-witness-positive.dfy" >> "%t"
// RUN: echo "well-foundedness, strict-descent and nonvacuity controls" >> "%t"
// RUN: %exits-with 4 %baredafny verify --modular-termination --decreases-orders --type-system-refresh --general-traits=datatype --standard-libraries:false --warn-unchecked-termination=false --show-snippets:false --use-basename-for-filename --solver-path %review-z3 --cores 1 --verification-time-limit 30 --resource-limit 16000000 "%S/Inputs/github-issue-245-witness-verify.dfy" >> "%t"
// RUN: echo "actual chains, function values and name spoofing" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --modular-termination --decreases-orders --type-system-refresh --general-traits=datatype --standard-libraries:false --warn-unchecked-termination=false --show-snippets:false --use-basename-for-filename "%S/Inputs/github-issue-245-witness-resolve.dfy" >> "%t"
// RUN: python3 "%S/Inputs/github-issue-245-witness-controls.py" "%binaryDir/Dafny.dll" "%t.dir" >> "%t"
// RUN: %diff "%s.expect" "%t"
