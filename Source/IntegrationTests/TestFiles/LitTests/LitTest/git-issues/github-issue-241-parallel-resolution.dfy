// RUN: ! %baredafny resolve --use-basename-for-filename --show-snippets:false --resolution-threads 1 %s > "%t.n1"
// RUN: ! %baredafny resolve --use-basename-for-filename --show-snippets:false --resolution-threads 2 %s > "%t.n2"
// RUN: ! %baredafny resolve --use-basename-for-filename --show-snippets:false --resolution-threads 8 %s > "%t.n8"
// RUN: %diff "%s.expect" "%t.n1"
// RUN: %diff "%t.n1" "%t.n2"
// RUN: %diff "%t.n1" "%t.n8"
// RUN: %baredafny verify --verify-included-files --cores 1 --resolution-threads 1 --bprint "%t.n1.bpl" "%s.valid.dfy" > "%t.v1"
// Compare the actual per-module Boogie files, including generated identifiers.
// RUN: %baredafny verify --verify-included-files --cores 1 --resolution-threads 2 --bprint "%t.n2.bpl" "%s.valid.dfy" > "%t.v2"
// RUN: %diff "%t.n1_M.bpl" "%t.n2_M.bpl"
// RUN: %baredafny verify --verify-included-files --cores 1 --resolution-threads 8 --bprint "%t.n8.bpl" "%s.valid.dfy" > "%t.v8"
// RUN: %diff "%t.n1_M.bpl" "%t.n8_M.bpl"
// RUN: %diff "%t.v1" "%t.v2"
// RUN: %diff "%t.v1" "%t.v8"
// RUN: %baredafny verify --verify-included-files --cores 1 --resolution-threads 8 --bprint "%t.unrelated.bpl" "%s.unrelated.dfy" > "%t.unrelated"
// RUN: %diff "%t.n1_M.bpl" "%t.unrelated_M.bpl"

// https://github.com/erniecohen/dafny/issues/241

module Base { function F(x: int): int { x + 1 } }
module W0 { import B = Base function F(x: int): int { B.F(x) } }
module W1 { import B = Base function F(x: int): int { B.F(x) } }
module W2 { import B = Base function F(x: int): int { B.F(x) } }
module W3 { import B = Base function F(x: int): int { B.F(x) } }
module W4 { import B = Base function F(x: int): int { B.F(x) } }
module W5 { import B = Base function F(x: int): int { B.F(x) } }
module W6 { import B = Base function F(x: int): int { B.F(x) } }
module W7 { import B = Base function F(x: int): int { B.F(x) } }
module W8 { import B = Base function F(x: int): int { B.F(x) } }
module W9 { import B = Base function F(x: int): int { B.F(x) } }
module W10 { import B = Base function F(x: int): int { B.F(x) } }
module W11 { import B = Base function F(x: int): int { B.F(x) } }
module Join { import A = W0 import B = W5 import C = W11 }
module ErrorA { function F(): int { false } }
module ErrorB { function F(): bool { 0 } }
module ErrorC { import A = W1 function F(): int { A.F(true) } }

// RUN: ! %baredafny resolve --use-basename-for-filename --show-snippets:false --resolution-threads 4 %s > "%t.n4"
// RUN: %diff "%t.n1" "%t.n4"
// RUN: %baredafny verify --verify-included-files --cores 1 --resolution-threads 4 --bprint "%t.n4.bpl" "%s.valid.dfy" > "%t.v4"
// RUN: %diff "%t.n1_M.bpl" "%t.n4_M.bpl"
// RUN: %diff "%t.v1" "%t.v4"
// RUN: %baredafny verify --verify-included-files --cores 1 --resolution-threads 4 --bprint "%t.shifted.bpl" "%s.shifted.dfy" > "%t.shifted"
// RUN: %diff "%t.n1_M.bpl" "%t.shifted_M.bpl"

// Rejected legacy/newtype configuration: report the same 19 errors as the
// shipped serial path, without traversing unresolved translation inputs.
// RUN: %exits-with 2 %baredafny verify --use-basename-for-filename --show-snippets:false --type-system-refresh:false --additional-axioms:false --consistent-obligation-checks:false --filter-symbol Std.Base64.EncodeBVIsBase64. --cores 1 --verification-time-limit 0 "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" > "%t.rejected.off"
// RUN: %exits-with 2 %baredafny verify --use-basename-for-filename --show-snippets:false --type-system-refresh:false --additional-axioms:false --consistent-obligation-checks:false --filter-symbol Std.Base64.EncodeBVIsBase64. --cores 1 --verification-time-limit 0 --resolution-threads 1 "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" > "%t.rejected.1"
// RUN: %exits-with 2 %baredafny verify --use-basename-for-filename --show-snippets:false --type-system-refresh:false --additional-axioms:false --consistent-obligation-checks:false --filter-symbol Std.Base64.EncodeBVIsBase64. --cores 1 --verification-time-limit 0 --resolution-threads 2 "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" > "%t.rejected.2"
// RUN: %exits-with 2 %baredafny verify --use-basename-for-filename --show-snippets:false --type-system-refresh:false --additional-axioms:false --consistent-obligation-checks:false --filter-symbol Std.Base64.EncodeBVIsBase64. --cores 1 --verification-time-limit 0 --resolution-threads 4 "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" > "%t.rejected.4"
// RUN: %exits-with 2 %baredafny verify --use-basename-for-filename --show-snippets:false --type-system-refresh:false --additional-axioms:false --consistent-obligation-checks:false --filter-symbol Std.Base64.EncodeBVIsBase64. --cores 1 --verification-time-limit 0 --resolution-threads 8 "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" > "%t.rejected.8"
// RUN: %diff "%t.rejected.off" "%t.rejected.1"
// RUN: %diff "%t.rejected.off" "%t.rejected.2"
// RUN: %diff "%t.rejected.off" "%t.rejected.4"
// RUN: %diff "%t.rejected.off" "%t.rejected.8"

// Canonical builtin identities survive operator-export cloning; hidden user
// members and opaque rank views remain rejected. Compare full diagnostics.
// RUN: %baredafny verify --cores 1 --verification-time-limit 0 --resource-limit 16000000 --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh "%S/Inputs/github-issue-194-revealed.dfy" > "%t.operators.revealed.off"
// RUN: %baredafny verify --cores 1 --verification-time-limit 0 --resource-limit 16000000 --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 1 "%S/Inputs/github-issue-194-revealed.dfy" > "%t.operators.revealed.1"
// RUN: %baredafny verify --cores 1 --verification-time-limit 0 --resource-limit 16000000 --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 2 "%S/Inputs/github-issue-194-revealed.dfy" > "%t.operators.revealed.2"
// RUN: %baredafny verify --cores 1 --verification-time-limit 0 --resource-limit 16000000 --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 4 "%S/Inputs/github-issue-194-revealed.dfy" > "%t.operators.revealed.4"
// RUN: %baredafny verify --cores 1 --verification-time-limit 0 --resource-limit 16000000 --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 8 "%S/Inputs/github-issue-194-revealed.dfy" > "%t.operators.revealed.8"
// RUN: %diff "%t.operators.revealed.off" "%t.operators.revealed.1"
// RUN: %diff "%t.operators.revealed.off" "%t.operators.revealed.2"
// RUN: %diff "%t.operators.revealed.off" "%t.operators.revealed.4"
// RUN: %diff "%t.operators.revealed.off" "%t.operators.revealed.8"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh "%S/Inputs/github-issue-194-hidden-member.dfy" > "%t.operators.hidden-member.off"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 1 "%S/Inputs/github-issue-194-hidden-member.dfy" > "%t.operators.hidden-member.1"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 2 "%S/Inputs/github-issue-194-hidden-member.dfy" > "%t.operators.hidden-member.2"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 4 "%S/Inputs/github-issue-194-hidden-member.dfy" > "%t.operators.hidden-member.4"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 8 "%S/Inputs/github-issue-194-hidden-member.dfy" > "%t.operators.hidden-member.8"
// RUN: %diff "%t.operators.hidden-member.off" "%t.operators.hidden-member.1"
// RUN: %diff "%t.operators.hidden-member.off" "%t.operators.hidden-member.2"
// RUN: %diff "%t.operators.hidden-member.off" "%t.operators.hidden-member.4"
// RUN: %diff "%t.operators.hidden-member.off" "%t.operators.hidden-member.8"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh "%S/Inputs/github-issue-194-opaque-rank.dfy" > "%t.operators.opaque-rank.off"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 1 "%S/Inputs/github-issue-194-opaque-rank.dfy" > "%t.operators.opaque-rank.1"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 2 "%S/Inputs/github-issue-194-opaque-rank.dfy" > "%t.operators.opaque-rank.2"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 4 "%S/Inputs/github-issue-194-opaque-rank.dfy" > "%t.operators.opaque-rank.4"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --user-defined-operators --type-system-refresh --resolution-threads 8 "%S/Inputs/github-issue-194-opaque-rank.dfy" > "%t.operators.opaque-rank.8"
// RUN: %diff "%t.operators.opaque-rank.off" "%t.operators.opaque-rank.1"
// RUN: %diff "%t.operators.opaque-rank.off" "%t.operators.opaque-rank.2"
// RUN: %diff "%t.operators.opaque-rank.off" "%t.operators.opaque-rank.4"
// RUN: %diff "%t.operators.opaque-rank.off" "%t.operators.opaque-rank.8"
