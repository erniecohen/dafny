// CommonJS extern modules retain their exported objects inside recursive groups.
// NONUNIFORM: JavaScript linkage compared with independent C# emission.
// RUN: python3 "%repositoryRoot/.github/review/recursive-module-js-require.py" "%binaryDir/Dafny.dll" "%review-z3" "%S/Inputs" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
