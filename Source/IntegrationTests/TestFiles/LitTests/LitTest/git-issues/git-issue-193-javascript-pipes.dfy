// JavaScript child output must drain even when both redirected pipes fill.
// RUN: python3 "%repositoryRoot/.github/review/javascript-process-pipes.py" "%binaryDir/Dafny.dll" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
method Main() { }
