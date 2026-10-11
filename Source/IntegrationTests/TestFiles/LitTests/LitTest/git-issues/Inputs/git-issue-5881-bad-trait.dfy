trait Ops { function value(): int }
datatype Box<O> = Box(x:O) { const o := x as Ops }
