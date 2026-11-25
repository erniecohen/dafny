// RUN: %baredafny verify --help-internal > "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
// CHECK: --rprint <file>
// CHECK: Executable compilation supports the C#, Java, Go, Python and Rust targets; Python and Rust groups may not contain extern declarations.
