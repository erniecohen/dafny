method Signs() {
  var positive := 5;
  var negative := -5;
  assert positive / 2 == 2;
  assert positive / (-2) == -2;
  assert negative / 2 == -3;
  assert negative / (-2) == 3;
  assert positive % 2 == 1;
  assert positive % (-2) == 1;
  assert negative % 2 == 1;
  assert negative % (-2) == 1;
}
