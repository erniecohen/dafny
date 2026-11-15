module Definition {
  export provides Positive, Create
  datatype D = D(value:int)
  type Positive = d:D | 0 < d.value witness D(1)
  function Create():Positive { D(1) }
}
module Client {
  import Definition
  function Cast():Definition.Positive { Definition.Create() as Definition.Positive }
}
