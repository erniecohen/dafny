// A pattern identifier that names a constructor is still a constructor when the source's element type
// is a datatype that inference has not fixed yet: if Red were a bound variable, Pick() would be 1.
datatype Option<+U> = None | Some(val: U)
datatype Color = Red | Green

function Pick(): int {
  var c := Some(Green);
  match c
  case Some(Red) => 1
  case Some(Green) => 2
  case None => 3
}

lemma PickRed()
  ensures Pick() == 2
{
}
