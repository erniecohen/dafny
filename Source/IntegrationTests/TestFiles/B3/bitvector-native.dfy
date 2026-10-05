lemma WordIdentities(x: bv3, y: bv3)
  ensures (x & x) == x
  ensures (x | y) == (y | x)
  ensures (x ^ x) == 0
  ensures !(!x) == x
  ensures (x + y) - y == x
  ensures x * 0 == 0
  ensures x < y || x >= y
{}

lemma Division(x: bv3, y: bv3)
  requires y != 0
  ensures x % y < y
  ensures (x / y) * y + x % y == x
{}

lemma Shifts(x: bv3)
  ensures x << 0 == x
  ensures x >> 0 == x
  ensures x << 3 == 0
  ensures x >> 3 == 0
{}

lemma ModularLiterals()
  ensures (7 as bv3) + 1 == 0
  ensures (4 as bv3) * 3 == 4
  ensures (7 as bv3) > (1 as bv3)
{}

lemma Resize(x: bv3)
  ensures ((x as bv7) as bv3) == x
{}

lemma Wide(x: bv67)
  ensures (x + (73786976294838206464 as bv67)) - (73786976294838206464 as bv67) == x
{}
