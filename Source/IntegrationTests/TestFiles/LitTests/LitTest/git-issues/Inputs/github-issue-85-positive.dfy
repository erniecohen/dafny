datatype Tree<T> = Leaf(value: T) | Fork(left: Tree<T>, right: Tree<T>)

lemma Identity<T>(x: T) ensures x == x { }

ghost function Patterns<T>(t: Tree<T>, x: T): T
{
  assert true by {
    match t {
      case Leaf(x) => Identity(x);
      case Fork(Leaf(x), right) =>
        match right {
          case Leaf(y) => Identity(y);
          case Fork(_, _) => Identity(x);
        }
      case Fork(Fork(_, _), _) => Identity(x);
    }
    Identity(x);
  }
  x
}

ghost function Literals(n: int): int
{
  assert n == n by {
    match n {
      case 0 => assert n == 0;
      case 1 | 2 => assert n == 1 || n == 2;
      case _ => assert n != 0;
    }
  }
  n
}

class Cell {
  var value: int

  twostate function Snapshot(t: Tree<int>): int
    reads this
  {
    assert true by {
      match t {
        case Leaf(x) => assert old(value) == old(value);
        case Fork(_, _) => assert value == value;
      }
    }
    old(value)
  }
}
