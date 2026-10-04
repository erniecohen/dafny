function {:fuel 0, 1} Down(n: nat): nat
  decreases n
{
  if n == 0 then 0 else Down(n - 1)
}

lemma {:fuel Down, 1} NativeFuel() {
  assert Down(0) == 0;
}
