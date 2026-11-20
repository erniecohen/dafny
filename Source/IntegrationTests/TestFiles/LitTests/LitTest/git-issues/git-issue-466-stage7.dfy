// Recursive module groups compile to C# namespaces, including after a library round trip.
// NONUNIFORM: C# is the first executable backend to support recursive module groups.
// RUN: %run --target cs --member-invariants --recursive-modules --verify-included-files "%S/Inputs/git-issue-466-stage7-source.dfy" --output "%t-source" > "%t"
// RUN: %run --target cs --member-invariants --recursive-modules --verify-included-files --resolution-threads 3 --optimize-erasable-datatype-wrapper:false "%S/Inputs/git-issue-466-stage7-source.dfy" --output "%t-unerased" >> "%t"
// RUN: %build -t:lib --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage7-library.dfy" --output "%t.doo" >> "%t"
// RUN: %run --target cs --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage7-client.dfy" --input "%t.doo" --output "%t-library" >> "%t"
// RUN: %exits-with 4 %run --target cs --member-invariants --recursive-modules "%S/Inputs/git-issue-466-stage7-negative.dfy" --output "%t-negative" >> "%t"
// RUN: %diff "%s.expect" "%t"
