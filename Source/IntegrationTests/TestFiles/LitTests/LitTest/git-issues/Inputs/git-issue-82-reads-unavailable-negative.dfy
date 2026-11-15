// Current member selection alone does not establish the .reads precondition.
class ReadCell {
  var objects: set<object?>
  ghost function ReadInput(n: int): int
    reads this, objects
  { n }
}
ghost function ReadAll(c: ReadCell): int
  reads c, c.ReadInput.reads
{ 0 }
