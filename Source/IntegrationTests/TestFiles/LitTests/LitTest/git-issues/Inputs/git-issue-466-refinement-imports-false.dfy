// A copied, renamed function retains its definition; a group cannot prove a false result.
module Original {
  function Id(x: int): (r: int) { x }
}
module Renamed refines Original {
  function Id(value: int): (result: int)
}
module Left {
  import Right
  import opened Copy := Renamed
  function Read(c: Right.Box): int { Id(x := c.value) }
  lemma Bad(c: Right.Box)
    ensures Read(c) == c.value + 1
  { }
}
module Right {
  import Left
  datatype Box = Box(value: int)
  lemma Bad(c: Box)
    ensures Left.Read(c) == c.value + 1
  { }
}
