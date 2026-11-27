#![allow(nonstandard_style)]
pub mod Foreign {
    pub mod Types {
        pub struct Actual {}
        impl Actual {
            pub fn _allocate_object() -> dafny_runtime::Object<Self> {
                unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self {})) }
            }
            pub fn Read(&self) -> dafny_runtime::DafnyInt { dafny_runtime::int!(13) }
        }
    }
    pub mod Tools {
        pub fn Echo(x: &std::rc::Rc<crate::A::Tree<dafny_runtime::DafnyInt>>)
          -> std::rc::Rc<crate::A::Tree<dafny_runtime::DafnyInt>> { x.clone() }
        pub fn Identity(x: &dafny_runtime::DafnyInt) -> dafny_runtime::DafnyInt { x.clone() }
    }
}
pub mod NativeOnly {
    pub struct Opaque {}
    pub struct _default {}
    impl _default {
        pub fn Zero() -> dafny_runtime::DafnyInt { dafny_runtime::int!(0) }
    }
}
impl crate::Foreign::Types::Implementation {
    pub fn ExternalEval(&self) -> dafny_runtime::DafnyInt { dafny_runtime::int!(17) }
}
