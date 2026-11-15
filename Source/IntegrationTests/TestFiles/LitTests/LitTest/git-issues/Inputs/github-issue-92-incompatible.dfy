datatype D = D(value:int)
datatype Other = Other(value:int)
type Positive = d:D | 0 < d.value witness D(1)
function Bad():Positive { Other(1) as Positive }
