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
