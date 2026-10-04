// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes
// Existing-language control: this declaration is already cyclic without the extension.
newtype N = seq<N>
