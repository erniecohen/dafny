// Commands requiring unsupported capabilities must reject both direct and project B3 selection.
// RUN: %exits-with 1 %baredafny measure-complexity "%s" --verification-backend b3 > "%t"
// RUN: %exits-with 1 %baredafny generate-tests Block "%s" --verification-backend b3 >> "%t"
// RUN: %exits-with 1 %baredafny find-dead-code "%s" --verification-backend b3 >> "%t"
// RUN: %exits-with 1 %baredafny measure-complexity "%S/Inputs/git-issue-126-b3.toml" --filter-symbol Absent >> "%t"
// RUN: %exits-with 1 %baredafny generate-tests Block "%S/Inputs/git-issue-126-b3.toml" >> "%t"
// RUN: %exits-with 1 %baredafny find-dead-code "%S/Inputs/git-issue-126-b3.toml" >> "%t"
// RUN: %diff "%s.expect" "%t"

method Positive() { assert true; }
