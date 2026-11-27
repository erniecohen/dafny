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

macro_rules! foreign_box {
    () => {
        pub struct Box<T: dafny_runtime::DafnyType> { marker: std::marker::PhantomData<T> }
        impl<T: dafny_runtime::DafnyType> Box<T> {
            pub fn _allocate_object() -> dafny_runtime::Object<Self> {
                unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self { marker: std::marker::PhantomData })) }
            }
            pub fn Echo(&self, value: &T) -> T { value.clone() }
        }
    };
}
pub mod Imported {
    foreign_box!();
    pub struct Native {}
    impl Native {
        pub fn _allocate_object() -> dafny_runtime::Object<Self> {
            unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self {})) }
        }
        pub fn Read(&self) -> dafny_runtime::DafnyInt { dafny_runtime::int!(31) }
    }
}

macro_rules! foreign_native {
    ($value:literal) => {
        pub struct Native {}
        impl Native {
            pub fn _allocate_object() -> dafny_runtime::Object<Self> {
                unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self {})) }
            }
            pub fn Read(&self) -> dafny_runtime::DafnyInt { dafny_runtime::int!($value) }
        }
    };
}
pub mod RefinedImported { foreign_native!(47); foreign_box!(); }
pub mod RefinedAgain { foreign_native!(59); foreign_box!(); }
pub mod Nested { pub mod r#async { foreign_native!(71); foreign_box!(); } }

pub mod Extended {
    foreign_box!();
    pub struct Native {}
    impl Native {
        pub fn _allocate_object() -> dafny_runtime::Object<Self> {
            unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self {})) }
        }
        pub fn Read(&self) -> dafny_runtime::DafnyInt { dafny_runtime::int!(89) }
        pub fn Extra(&self, x: &dafny_runtime::DafnyInt) -> dafny_runtime::DafnyInt { x.clone() + dafny_runtime::int!(100) }
    }
    pub struct Fresh<T: dafny_runtime::DafnyType> { marker: std::marker::PhantomData<T> }
    impl<T: dafny_runtime::DafnyType> Fresh<T> {
        pub fn _allocate_object() -> dafny_runtime::Object<Self> {
            unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self { marker: std::marker::PhantomData })) }
        }
        pub fn Echo(&self, x: &T) -> T { x.clone() }
    }
}
pub mod ExtendedAgain {
    foreign_box!();
    pub struct Native {}
    impl Native {
        pub fn _allocate_object() -> dafny_runtime::Object<Self> {
            unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self {})) }
        }
        pub fn Read(&self) -> dafny_runtime::DafnyInt { dafny_runtime::int!(97) }
        pub fn Extra(&self, x: &dafny_runtime::DafnyInt) -> dafny_runtime::DafnyInt { x.clone() + dafny_runtime::int!(100) }
        pub fn Second(&self, x: &dafny_runtime::DafnyInt) -> dafny_runtime::DafnyInt { x.clone() + dafny_runtime::int!(200) }
    }
    pub struct Fresh<T: dafny_runtime::DafnyType> { marker: std::marker::PhantomData<T> }
    impl<T: dafny_runtime::DafnyType> Fresh<T> {
        pub fn _allocate_object() -> dafny_runtime::Object<Self> {
            unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self { marker: std::marker::PhantomData })) }
        }
        pub fn Echo(&self, x: &T) -> T { x.clone() }
    }
}

macro_rules! partial_foreign {
    ($value:literal) => {
        foreign_box!();
        pub struct Native {}
        impl Native {
            pub fn _allocate_object() -> dafny_runtime::Object<Self> {
                unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self {})) }
            }
            pub fn Read(&self) -> dafny_runtime::DafnyInt { dafny_runtime::int!($value) }
            pub fn Extra(&self, x: &dafny_runtime::DafnyInt) -> dafny_runtime::DafnyInt { x.clone() + dafny_runtime::int!(100) }
        }
        pub struct Fresh<T: dafny_runtime::DafnyType> { marker: std::marker::PhantomData<T> }
        impl<T: dafny_runtime::DafnyType> Fresh<T> {
            pub fn _allocate_object() -> dafny_runtime::Object<Self> {
                unsafe { dafny_runtime::Object::from_rc(std::rc::Rc::new(Self { marker: std::marker::PhantomData })) }
            }
            pub fn Echo(&self, x: &T) -> T { x.clone() }
        }
    };
}
pub mod Partial { partial_foreign!(107); }
pub mod PartialAgain { partial_foreign!(109); }
