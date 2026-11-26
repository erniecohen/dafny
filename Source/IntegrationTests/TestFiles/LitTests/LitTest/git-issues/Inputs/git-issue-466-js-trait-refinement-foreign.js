// Native implementations retain preloaded trait objects before generated code runs.
(() => {
  ForeignB.NativeView = class {};
  ForeignB.NativeGenericView = class {};
  const view = ForeignB.NativeView;
  const genericView = ForeignB.NativeGenericView;
  ForeignB.NativeViewer = class {
    constructor(value) { this.value = value; }
    static Make(value) { return new this(value); }
    Read() {
      if (!this._parentTraits().includes(ForeignB.NativeGenericView)) {
        throw new Error("foreign generic trait identity replaced");
      }
      return this.value;
    }
    Value() { return new BigNumber(173); }
    _parentTraits() { return [view, genericView]; }
  };
})();
(() => {
  ForeignRefined.NativeView = class {};
  ForeignRefined.NativeGenericView = class {};
  const view = ForeignRefined.NativeView;
  const genericView = ForeignRefined.NativeGenericView;
  ForeignRefined.NativeViewer = class {
    constructor(value) { this.value = value; }
    static Make(value) { return new this(value); }
    Read() {
      if (!this._parentTraits().includes(ForeignRefined.NativeGenericView)) {
        throw new Error("foreign generic trait identity replaced");
      }
      return this.value;
    }
    Value() { return new BigNumber(179); }
    _parentTraits() { return [view, genericView]; }
  };
})();
(() => {
  ForeignFinal.NativeView = class {};
  ForeignFinal.NativeGenericView = class {};
  const view = ForeignFinal.NativeView;
  const genericView = ForeignFinal.NativeGenericView;
  ForeignFinal.NativeViewer = class {
    constructor(value) { this.value = value; }
    static Make(value) { return new this(value); }
    Read() {
      if (!this._parentTraits().includes(ForeignFinal.NativeGenericView)) {
        throw new Error("foreign generic trait identity replaced");
      }
      return this.value;
    }
    Value() { return new BigNumber(181); }
    _parentTraits() { return [view, genericView]; }
  };
})();
