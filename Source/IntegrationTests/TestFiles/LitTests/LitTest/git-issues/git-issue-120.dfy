// Invalid B3 configuration must fail even when symbol selection yields no work.
// RUN: %exits-with 4 %baredafny verify "%s" --verification-backend b3 --verification-time-limit 0 --solver-path "%review-z3" --filter-symbol Absent --show-snippets:false --use-basename-for-filename > "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s.expect"

method Negative() { assert false; }
