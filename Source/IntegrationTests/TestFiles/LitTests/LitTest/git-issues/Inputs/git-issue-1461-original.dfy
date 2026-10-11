// The issue's program, with its members in a class.
class C {
twostate predicate p() reads this
method test() {
      var q := p;
      assume p();  // no problem
      assume q();  // error: possible violation of function precondition
}
}
