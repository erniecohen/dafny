// RUN: python3 "%repositoryRoot/.github/review/persistent-ide-failure-reports.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
lemma Positive(x: int) { assert x == 0; }
