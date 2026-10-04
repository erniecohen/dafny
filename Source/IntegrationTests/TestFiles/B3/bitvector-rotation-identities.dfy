method Three(x: bv3)
  ensures x.RotateLeft(0) == x
  ensures x.RotateRight(3) == x
  ensures x.RotateLeft(1).RotateRight(1) == x
{}
method One(x: bv1)
  ensures x.RotateLeft(1) == x
  ensures x.RotateRight(0) == x
{}
method Wide(x: bv67)
  ensures x.RotateLeft(0) == x
  ensures x.RotateRight(67) == x
{}
