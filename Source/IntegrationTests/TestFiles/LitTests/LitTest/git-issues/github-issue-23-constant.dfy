// RUN: %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Instantiating a generic constant substitutes its enclosing type parameters.
// Without that substitution the reviewer observed an undeclared-type crash upstream.
ghost function G<X>(): bool { true }
class C<T> { ghost const k: bool := G<T>() }
method UseK(c: C<int>) { ghost var b := c.k; assert b; }
