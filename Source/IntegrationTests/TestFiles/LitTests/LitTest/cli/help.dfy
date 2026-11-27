// RUN: %baredafny verify --help-internal > "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
// CHECK: --rprint <file>
// CHECK: Executable compilation supports the C#, Java, Go, JavaScript, Python and Rust targets; JavaScript and Rust groups may not contain extern declarations. Python group members containing extern or omitted declarations require complete foreign Python modules; a Python package group must retain a compiled member. Rust group programs require explicit array initialization
