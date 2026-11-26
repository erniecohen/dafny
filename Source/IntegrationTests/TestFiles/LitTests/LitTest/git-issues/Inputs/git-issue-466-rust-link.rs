#[path = "producer-rust/src/producer.rs"]
pub mod producer;
#[path = "consumer-rust/src/consumer.rs"]
mod consumer_generated;
pub mod consumer {
    pub use crate::producer::*;
    pub use crate::consumer_generated::*;
}
fn main() {
    let value = producer::A::Tree::<dafny_runtime::DafnyInt>::Leaf { value: dafny_runtime::int!(42) };
    assert!(matches!(value, producer::A::Tree::Leaf { .. }));
    consumer::Client::_default::Main(&dafny_runtime::Sequence::from_array_owned(vec![]));
}
