datatype D = D(value:int)
codatatype Stream = S(head:int, tail:Stream)
type PositiveStream = s:Stream | 0 < s.head witness *
function Plain():D { D(1) as D }
function Co(s:Stream):PositiveStream { s as PositiveStream }
