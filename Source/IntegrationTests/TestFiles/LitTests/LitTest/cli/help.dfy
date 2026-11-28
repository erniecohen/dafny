// RUN: %baredafny verify --help-internal > "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
// CHECK: --rprint <file>
// CHECK: Executable compilation supports the C#, Java, Go, JavaScript, Python and Rust targets; JavaScript and Rust groups support extern linkage, with target-specific restrictions described in the reference manual. JavaScript extern modules may be preloaded objects or CommonJS libraries loaded with require. Python group members containing extern or omitted declarations require complete foreign Python modules; a Python package group must retain a compiled member. Rust group programs require explicit array initialization
