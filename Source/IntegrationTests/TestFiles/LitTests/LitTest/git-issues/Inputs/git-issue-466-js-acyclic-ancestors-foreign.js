// These acyclic declarations are ancestors of the group's imported traits.
let ForeignOutside = {
  NativeOutsideRoot: class {},
  NativeOutsideGenericRoot: class {},
  NativeUnused: class {}
};
const outsideRoot = ForeignOutside.NativeOutsideRoot;
const outsideGenericRoot = ForeignOutside.NativeOutsideGenericRoot;
const unused = ForeignOutside.NativeUnused;
// Native implementations retain preloaded trait objects before generated code runs.
(() => {
  ForeignB.NativeRoot = class {};
  ForeignB.NativeLeft = class {};
  ForeignB.NativeRight = class {};
  ForeignB.NativeGenericRoot = class {};
  ForeignB.NativeView = class {};
  ForeignB.NativeGenericView = class {};
  const root = ForeignB.NativeRoot;
  const left = ForeignB.NativeLeft;
  const right = ForeignB.NativeRight;
  const genericRoot = ForeignB.NativeGenericRoot;
  const view = ForeignB.NativeView;
  const genericView = ForeignB.NativeGenericView;
  ForeignB.NativeViewer = class {
    constructor(value) { this.value = value; }
    static Make(value) { return new this(value); }
    Read() {
      if (!this._parentTraits().includes(ForeignB.NativeGenericView) || !this._parentTraits().includes(ForeignB.NativeGenericRoot)) {
        throw new Error("foreign generic trait identity replaced");
      }
      if (!this._parentTraits().includes(ForeignOutside.NativeOutsideGenericRoot)) {
        throw new Error("acyclic generic ancestor identity replaced");
      }
      if (ForeignOutside.NativeUnused === unused) {
        throw new Error("unrelated acyclic trait identity preserved");
      }
      return this.value;
    }
    Value() { return new BigNumber(173); }
    RootValue() { return this.Value().plus(10); }
    _parentTraits() { return [outsideRoot, outsideGenericRoot, root, left, right, genericRoot, view, genericView]; }
  };
})();
(() => {
  ForeignRefined.NativeRoot = class {};
  ForeignRefined.NativeLeft = class {};
  ForeignRefined.NativeRight = class {};
  ForeignRefined.NativeGenericRoot = class {};
  ForeignRefined.NativeView = class {};
  ForeignRefined.NativeGenericView = class {};
  const root = ForeignRefined.NativeRoot;
  const left = ForeignRefined.NativeLeft;
  const right = ForeignRefined.NativeRight;
  const genericRoot = ForeignRefined.NativeGenericRoot;
  const view = ForeignRefined.NativeView;
  const genericView = ForeignRefined.NativeGenericView;
  ForeignRefined.NativeViewer = class {
    constructor(value) { this.value = value; }
    static Make(value) { return new this(value); }
    Read() {
      if (!this._parentTraits().includes(ForeignRefined.NativeGenericView) || !this._parentTraits().includes(ForeignRefined.NativeGenericRoot)) {
        throw new Error("foreign generic trait identity replaced");
      }
      if (!this._parentTraits().includes(ForeignOutside.NativeOutsideGenericRoot)) {
        throw new Error("acyclic generic ancestor identity replaced");
      }
      if (ForeignOutside.NativeUnused === unused) {
        throw new Error("unrelated acyclic trait identity preserved");
      }
      return this.value;
    }
    Value() { return new BigNumber(179); }
    RootValue() { return this.Value().plus(10); }
    _parentTraits() { return [outsideRoot, outsideGenericRoot, root, left, right, genericRoot, view, genericView]; }
  };
})();
(() => {
  ForeignFinal.NativeRoot = class {};
  ForeignFinal.NativeLeft = class {};
  ForeignFinal.NativeRight = class {};
  ForeignFinal.NativeGenericRoot = class {};
  ForeignFinal.NativeView = class {};
  ForeignFinal.NativeGenericView = class {};
  const root = ForeignFinal.NativeRoot;
  const left = ForeignFinal.NativeLeft;
  const right = ForeignFinal.NativeRight;
  const genericRoot = ForeignFinal.NativeGenericRoot;
  const view = ForeignFinal.NativeView;
  const genericView = ForeignFinal.NativeGenericView;
  ForeignFinal.NativeViewer = class {
    constructor(value) { this.value = value; }
    static Make(value) { return new this(value); }
    Read() {
      if (!this._parentTraits().includes(ForeignFinal.NativeGenericView) || !this._parentTraits().includes(ForeignFinal.NativeGenericRoot)) {
        throw new Error("foreign generic trait identity replaced");
      }
      if (!this._parentTraits().includes(ForeignOutside.NativeOutsideGenericRoot)) {
        throw new Error("acyclic generic ancestor identity replaced");
      }
      if (ForeignOutside.NativeUnused === unused) {
        throw new Error("unrelated acyclic trait identity preserved");
      }
      return this.value;
    }
    Value() { return new BigNumber(181); }
    RootValue() { return this.Value().plus(10); }
    _parentTraits() { return [outsideRoot, outsideGenericRoot, root, left, right, genericRoot, view, genericView]; }
  };
})();
