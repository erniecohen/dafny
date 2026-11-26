module A {
  import B
  datatype Left = End | Next(right: B.Right)
}
module B {
  import A
  datatype Right = Right(left: A.Left)
}
method Main() {
  var left := A.Next(B.Right(A.End));
  print left.right.left.End?, "\n";
}
