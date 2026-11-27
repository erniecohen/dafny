// RUN: python3 "%repositoryRoot/.github/review/persistent-ide.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
lemma Positive(x: int) ensures x + 1 > x {}
