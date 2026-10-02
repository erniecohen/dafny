newtype Id<T> = x: T | true witness *
newtype Twice<T> = Id<Id<T>>
