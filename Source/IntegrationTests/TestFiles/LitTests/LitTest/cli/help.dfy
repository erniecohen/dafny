// RUN: %baredafny verify --help-internal > "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
// CHECK: --rprint <file>
// CHECK: Executable compilation supports the C#, Java, Go and Python targets; Go groups may not contain extern declarations.
