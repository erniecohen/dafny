// RUN: %testDafnyForEachResolver "%s"


trait A extends object {
  predicate f()
  method g() ensures f()
}

class B<T> extends A {
  predicate f() { true }
  method g() ensures f()
}
