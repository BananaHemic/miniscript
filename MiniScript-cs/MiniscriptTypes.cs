﻿/*	MiniscriptTypes.cs

Classes in this file represent the MiniScript type system.  Value is the 
abstract base class for all of them (i.e., represents ANY value in MiniScript),
from which more specific types are derived.

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Collections;

namespace Miniscript {
	
	/// <summary>
	/// Value: abstract base class for the MiniScript type hierarchy.
	/// Defines a number of handy methods that you can call on ANY
	/// value (though some of these do nothing for some types).
	/// </summary>
	public abstract class Value {
		/// <summary>
		/// Get the current value of this Value in the given context.  Basic types
		/// evaluate to themselves, but some types (e.g. variable references) may
		/// evaluate to something else.
		/// </summary>
		/// <param name="context">TAC context to evaluate in</param>
		/// <returns>value of this value (possibly the same as this)</returns>
		public virtual Value Val(TAC.Context context, bool takeRef) {
			return this;		// most types evaluate to themselves
		}
		
		public override string ToString() {
			return ToString(null);
		}
		
		public abstract string ToString(TAC.Machine vm);
		
		/// <summary>
		/// This version of Val is like the one above, but also returns
		/// (via the output parameter) the ValMap the value was found in,
		/// which could be several steps up the __isa chain.
		/// </summary>
		/// <returns>The value.</returns>
		/// <param name="context">Context.</param>
		/// <param name="valueFoundIn">Value found in.</param>
		public virtual Value Val(TAC.Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			return this;
		}
		
		/// <summary>
		/// Similar to Val, but recurses into the sub-values contained by this
		/// value (if it happens to be a container, such as a list or map).
		/// </summary>
		/// <param name="context">context in which to evaluate</param>
		/// <returns>fully-evaluated value</returns>
		public virtual Value FullEval(TAC.Context context) {
			return this;
		}
		
		/// <summary>
		/// Get the numeric value of this Value as an integer.
		/// </summary>
		/// <returns>this value, as signed integer</returns>
		public virtual int IntValue() {
			return (int)DoubleValue();
		}
		
		/// <summary>
		/// Get the numeric value of this Value as an unsigned integer.
		/// </summary>
		/// <returns>this value, as unsigned int</returns>
		public virtual uint UIntValue() {
			return (uint)DoubleValue();
		}
		
		/// <summary>
		/// Get the numeric value of this Value as a single-precision float.
		/// </summary>
		/// <returns>this value, as a float</returns>
		public virtual float FloatValue() {
			return (float)DoubleValue();
		}
		
		/// <summary>
		/// Get the numeric value of this Value as a double-precision floating-point number.
		/// </summary>
		/// <returns>this value, as a double</returns>
		public virtual double DoubleValue() {
			return 0;				// most types don't have a numeric value
		}
		
		/// <summary>
		/// Get the boolean (truth) value of this Value.  By default, we consider
		/// any numeric value other than zero to be true.  (But subclasses override
		/// this with different criteria for strings, lists, and maps.)
		/// </summary>
		/// <returns>this value, as a bool</returns>
		public virtual bool BoolValue() {
			return IntValue() != 0;
		}
		
		/// <summary>
		/// Get this value in the form of a MiniScript literal.
		/// </summary>
		/// <param name="recursionLimit">how deeply we can recurse, or -1 for no limit</param>
		/// <returns></returns>
		public virtual string CodeForm(TAC.Machine vm, int recursionLimit=-1) {
			return ToString(vm);
		}
		
		/// <summary>
		/// Get a hash value for this Value.  Two values that are considered
		/// equal will return the same hash value.
		/// </summary>
		/// <returns>hash value</returns>
		public abstract int Hash(int recursionDepth=16);
		
		/// <summary>
		/// Check whether this Value is equal to another Value.
		/// </summary>
		/// <param name="rhs">other value to compare to</param>
		/// <returns>1if these values are considered equal; 0 if not equal; 0.5 if unsure</returns>
		public abstract double Equality(Value rhs, int recursionDepth=16);
		
		/// <summary>
		/// Can we set elements within this value?  (I.e., is it a list or map?)
		/// </summary>
		/// <returns>true if SetElem can work; false if it does nothing</returns>
		public virtual bool CanSetElem() { return false; }
		
		/// <summary>
		/// Set an element associated with the given index within this Value.
		/// </summary>
		/// <param name="index">index/key for the value to set</param>
		/// <param name="value">value to set</param>
		public virtual void SetElem(Value index, Value value) {}

		/// <summary>
		/// Return whether this value is the given type (or some subclass thereof)
		/// in the context of the given virtual machine.
		/// </summary>
		public virtual bool IsA(Value type, TAC.Machine vm) {
			return false;
		}

		public static int Compare(Value x, Value y) {
			// If either argument is a string, do a string comparison
			if (x is ValString || y is ValString) {
					var sx = x.ToString();
					var sy = y.ToString();
					return sx.CompareTo(sy);
			}
			// If both arguments are numbers, compare numerically
			if (x is ValNumber && y is ValNumber) {
				double fx = ((ValNumber)x).value;
				double fy = ((ValNumber)y).value;
				if (fx < fy) return -1;
				if (fx > fy) return 1;
				return 0;
			}
			// Otherwise, consider all values equal, for sorting purposes.
			return 0;
		}
	}

    public class ValuePool<T> where T : PoolableValue
    {
        private readonly Stack<T> _pool = new Stack<T>();
        public int Count { get { return _pool.Count; } }

        public T GetInstance()
        {
            if (_pool.Count == 0)
                return null;
            //Console.WriteLine("from pool");
            T val = _pool.Pop();
            // TODO sometimes we create a Value and immediately assign it to
            // a map. In this case, the ref count here should be 0!
            return val;
        }
        public void ReturnToPool(T poolableValue)
        {
            _pool.Push(poolableValue);
        }
    }

    /// <summary>
    /// In certain platforms, like Unity, memory allocation can
    /// be very slow. To help alleviate this, Minscript values
    /// are pooled in a thread static stack of unused values
    /// </summary>
    public abstract class PoolableValue : Value
    {
        protected int _refCount = 1;
        protected readonly bool _poolable;

        protected PoolableValue(bool usePool)
        {
            _poolable = usePool;
        }
        protected abstract void ResetState();
        protected abstract void ReturnToPool();
        public virtual void Ref()
        {
            if (_poolable)
                _refCount++;
        }
        public int GetRefCount()
        {
            return _refCount;
        }
        public virtual void Unref()
        {
            if (!_poolable)
                return;

            _refCount--;
            if (_refCount > 0)
                return;
            else if (_refCount < 0)
            {
                Console.WriteLine("Extra unref! For " + GetType().ToString());
                return;
            }
            ResetState();
            ReturnToPool();
            //Console.WriteLine("into pool " + GetType().ToString());
        }
        public override Value Val(TAC.Context context, bool takeRef)
        {
            //TODO I think that this is wrong, sometimes
            // I believe that Val returns a new Value,
            // instead of the existing one
            //Console.WriteLine("valref");
            if(takeRef)
                Ref();
            return base.Val(context, takeRef);
        }
        public override Value Val(TAC.Context context, out ValMap valueFoundIn)
        {
            //Console.WriteLine("valref 2");
            Ref();
            return base.Val(context, out valueFoundIn);
        }
    }
	
	public class ValueSorter : IComparer<Value> {
		public static ValueSorter instance = new ValueSorter();
		public int Compare(Value x, Value y) {
			return Value.Compare(x, y);
		}
	}

	/// <summary>
	/// ValNull is an object to represent null in places where we can't use
	/// an actual null (such as a dictionary key or value).
	/// </summary>
	public class ValNull : Value {
		private ValNull() {}
		
		public override string ToString(TAC.Machine machine) {
			return "null";
		}
		
		public override bool IsA(Value type, TAC.Machine vm) {
			return false;
		}

		public override int Hash(int recursionDepth=16) {
			return -1;
		}

		public override Value Val(TAC.Context context, bool takeRef) {
			return null;
		}

		public virtual Value Val(TAC.Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			return null;
		}
		
		public virtual Value FullEval(TAC.Context context) {
			return null;
		}
		
		public override int IntValue() {
			return 0;
		}

		public override double DoubleValue() {
			return 0.0;
		}
		
		public override bool BoolValue() {
			return false;
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return (rhs == null || rhs is ValNull ? 1 : 0);
		}

		static readonly ValNull _inst = new ValNull();
		
		/// <summary>
		/// Handy accessor to a shared "instance".
		/// </summary>
		public static ValNull instance { get { return _inst; } }
		
	}
	
	/// <summary>
	/// ValNumber represents a numeric (double-precision floating point) value in MiniScript.
	/// Since we also use numbers to represent boolean values, ValNumber does that job too.
	/// </summary>
	public class ValNumber : PoolableValue {
		public double value { get; private set; }
        [ThreadStatic]
        protected static ValuePool<ValNumber> _valuePool;
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }

		private ValNumber(double value, bool usePool) : base(usePool) {
			this.value = value;
		}
        public static ValNumber Create(double value)
        {
            if (_valuePool == null)
                _valuePool = new ValuePool<ValNumber>();
            else
            {
                ValNumber val = _valuePool.GetInstance();
                if (val != null)
                {
                    val._refCount = 1;
                    val.value = value;
                    return val;
                }
            }
            _numInstancesAllocated++;
            return new ValNumber(value, true);
        }
        public override void Unref()
        {
            if (base._refCount == 1)
            {
                //Console.WriteLine("Recyclying val " + value);
            }
            base.Unref();
        }
        public override void Ref()
        {
            base.Ref();
        }
        //public override Value Val(TAC.Context context)
        //{
        //    int prevRef = _refCount;
        //    Value v = base.Val(context);
        //    //Console.WriteLine("Due to val, ref increased from " + prevRef + " to " + _refCount + " value " + value);
        //    if (value == 5)
        //    {
        //        Console.WriteLine("Due to val, ref increased from " + prevRef + " to " + _refCount + " value " + value);
        //    }
        //    return v;
        //}
        protected override void ResetState() {}
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValNumber>();
            _valuePool.ReturnToPool(this);
        }

        public override string ToString(TAC.Machine vm) {
			// Convert to a string in the standard MiniScript way.
			if (value % 1.0 == 0.0) {
				// integer values as integers
				return value.ToString("0", CultureInfo.InvariantCulture);
			} else if (value > 1E10 || value < -1E10 || (value < 1E-6 && value > -1E-6)) {
				// very large/small numbers in exponential form
				return value.ToString("E6", CultureInfo.InvariantCulture);
			} else {
				// all others in decimal form, with 1-6 digits past the decimal point
				return value.ToString("0.0#####", CultureInfo.InvariantCulture);
			}
		}

		public override int IntValue() {
			return (int)value;
		}

		public override double DoubleValue() {
			return value;
		}
		
		public override bool BoolValue() {
			// Any nonzero value is considered true, when treated as a bool.
			return value != 0;
		}

		public override bool IsA(Value type, TAC.Machine vm) {
			return type == vm.numberType;
		}

		public override int Hash(int recursionDepth=16) {
			return value.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return rhs is ValNumber && ((ValNumber)rhs).value == value ? 1 : 0;
		}

		static ValNumber _zero = new ValNumber(0, false), _one = new ValNumber(1, false);
		
		/// <summary>
		/// Handy accessor to a shared "zero" (0) value.
		/// IMPORTANT: do not alter the value of the object returned!
		/// </summary>
		public static ValNumber zero { get { return _zero; } }
		
		/// <summary>
		/// Handy accessor to a shared "one" (1) value.
		/// IMPORTANT: do not alter the value of the object returned!
		/// </summary>
		public static ValNumber one { get { return _one; } }
		
		/// <summary>
		/// Convenience method to get a reference to zero or one, according
		/// to the given boolean.  (Note that this only covers Boolean
		/// truth values; MiniScript also allows fuzzy truth values, like
		/// 0.483, but obviously this method won't help with that.)
		/// IMPORTANT: do not alter the value of the object returned!
		/// </summary>
		/// <param name="truthValue">whether to return 1 (true) or 0 (false)</param>
		/// <returns>ValNumber.one or ValNumber.zero</returns>
		public static ValNumber Truth(bool truthValue) {
			return truthValue ? one : zero;
		}
		
		/// <summary>
		/// Basically this just makes a ValNumber out of a double,
		/// BUT it is optimized for the case where the given value
		///	is either 0 or 1 (as is usually the case with truth tests).
		/// </summary>
		public static ValNumber Truth(double truthValue) {
			if (truthValue == 0.0) return zero;
			if (truthValue == 1.0) return one;
			return ValNumber.Create(truthValue);
		}

    }
	
	/// <summary>
	/// ValString represents a string (text) value.
	/// </summary>
	public class ValString : PoolableValue {
		public static long maxSize = 0xFFFFFF;		// about 16M elements
		
		public string value { get; protected set; }
        [ThreadStatic]
        protected static ValuePool<ValString> _valuePool;
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }
        [ThreadStatic]
        private static StringBuilder _workingSbA;
        [ThreadStatic]
        private static StringBuilder _workingSbB;

        //TODO add create with ValString for fast add
        public static ValString Create(string val, bool usePool=true) {
            if(!usePool)
                return new ValString(val, false);

            if (_valuePool == null)
                _valuePool = new ValuePool<ValString>();
            else
            {
                ValString valStr = _valuePool.GetInstance();
                if (valStr != null)
                {
                    valStr._refCount = 1;
                    valStr.value = val;
                    return valStr;
                }
            }

            _numInstancesAllocated++;
            return new ValString(val, true);
        }
		protected ValString(string value, bool usePool) : base(usePool) {
			this.value = value ?? _empty.value;
            //base._refCount = 0;
		}
        public override void Ref()
        {
            base.Ref();
        }
        public override void Unref()
        {
            base.Unref();
            //Console.WriteLine("Str unref, ref count #" + _refCount);
        }
        protected override void ResetState()
        {
            value = null;
            //Console.WriteLine("Str back in pool");
        }
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValString>();
            _valuePool.ReturnToPool(this);
        }

        public override string ToString(TAC.Machine vm) {
			return value;
		}

		public override string CodeForm(TAC.Machine vm, int recursionLimit=-1) {
            if (_workingSbA == null)
                _workingSbA = new StringBuilder();
            else
                _workingSbA.Clear();
            if (_workingSbB == null)
                _workingSbB = new StringBuilder();
            else
                _workingSbB.Clear();
            _workingSbA.Append("\"");
            _workingSbB.Append(value);
            _workingSbB.Replace("\"", "\"\"");
            _workingSbA.Append(_workingSbB);
            _workingSbA.Append("\"");
            return _workingSbA.ToString();
			//return "\"" + value.Replace("\"", "\"\"") + "\"";
		}

		public override bool BoolValue() {
			// Any nonempty string is considered true.
			return !string.IsNullOrEmpty(value);
		}

		public override bool IsA(Value type, TAC.Machine vm) {
			return type == vm.stringType;
		}

		public override int Hash(int recursionDepth=16) {
			return value.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			// String equality is treated the same as in C#.
			return rhs is ValString && ((ValString)rhs).value == value ? 1 : 0;
		}

		public Value GetElem(Value index) {
			var i = index.IntValue();
			if (i < 0) i += value.Length;
			if (i < 0 || i >= value.Length) {
				throw new IndexException("Index Error (string index " + index + " out of range)");

			}
			return ValString.Create(value.Substring(i, 1));
		}

		// Magic identifier for the is-a entry in the class system:
		public static ValString magicIsA = new ValString("__isa", false);

		public static ValString selfStr = new ValString("self", false);
		
		static ValString _empty = new ValString("", false);
		
		/// <summary>
		/// Handy accessor for an empty ValString.
		/// IMPORTANT: do not alter the value of the object returned!
		/// </summary>
		public static ValString empty { get { return _empty; } }

	}
	
	// We frequently need to generate a ValString out of a string for fleeting purposes,
	// like looking up an identifier in a map (which we do ALL THE TIME).  So, here's
	// a little recycling pool of reusable ValStrings, for this purpose only.
	class TempValString : ValString {
		private TempValString next;

		private TempValString(string s) : base(s, false) {
			this.next = null;
		}

		private static TempValString _tempPoolHead = null;
		private static object lockObj = new object();
		public static TempValString Get(string s) {
			lock(lockObj) {
				if (_tempPoolHead == null) {
					return new TempValString(s);
				} else {
					var result = _tempPoolHead;
					_tempPoolHead = _tempPoolHead.next;
					result.value = s;
					return result;
				}
			}
		}
		public static void Release(TempValString temp) {
			lock(lockObj) {
				temp.next = _tempPoolHead;
				_tempPoolHead = temp;
			}
		}
	}
	
	
	/// <summary>
	/// ValList represents a MiniScript list (which, under the hood, is
	/// just a wrapper for a List of Values).
	/// </summary>
	public class ValList : PoolableValue {
		public static long maxSize = 0xFFFFFF;      // about 16 MB

        public int Count { get { return values.Count; } }
        public readonly List<Value> values;
        [ThreadStatic]
        private static ValuePool<ValList> _valuePool;
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }
        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;

        public static ValList Create(int capacity=0)
        {
            //Console.WriteLine("ValList create");
            if (_valuePool == null)
                _valuePool = new ValuePool<ValList>();
            else
            {
                ValList existing = _valuePool.GetInstance();
                if(existing != null)
                {
                    existing._refCount = 1;
                    existing.EnsureCapacity(capacity);
                    return existing;
                }
            }

            _numInstancesAllocated++;
            return new ValList(capacity, true);
        }
        public override void Ref()
        {
            base.Ref();
        }
        public override void Unref()
        {
            base.Unref();
        }
        protected override void ResetState()
        {
            for(int i = 0; i < values.Count;i++)
            {
                PoolableValue valPool = values[i] as PoolableValue;
                if (valPool != null)
                    valPool.Unref();
            }
            //Console.WriteLine("ValList back in pool");
            values.Clear();
        }
        private ValList(int capacity, bool poolable) : base(poolable) {
            values = new List<Value>(capacity);
		}
        public void Add(Value value, bool takeRef=true)
        {
            if (takeRef)
            {
                PoolableValue valPool = value as PoolableValue;
                if (valPool != null)
                    valPool.Ref();
            }
            values.Add(value);
        }
        public void SetToList(List<Value> recvValues)
        {
            ResetState();
            for(int i = 0; i < recvValues.Count;i++)
            {
                Value value = recvValues[i];
                PoolableValue valPool = value as PoolableValue;
                if (valPool != null)
                    valPool.Ref();
                values.Add(value);
            }
        }
        public void EnsureCapacity(int capacity)
        {
            if (values.Capacity < capacity)
                values.Capacity = capacity; //TODO maybe enfore this being a PoT?
        }
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValList>();
            _valuePool.ReturnToPool(this);
        }
        public void Insert(int idx, Value value)
        {
            PoolableValue poolableValue = value as PoolableValue;
            if (poolableValue != null)
                poolableValue.Ref();
            values.Insert(idx, value);
        }
        public override Value FullEval(TAC.Context context) {
			// Evaluate each of our list elements, and if any of those is
			// a variable or temp, then resolve those now.
			// CAUTION: do not mutate our original list!  We may need
			// it in its original form on future iterations.
			ValList result = null;
			for (var i = 0; i < values.Count; i++) {
				var copied = false;
				if (values[i] is ValTemp || values[i] is ValVar) {
					Value newVal = values[i].Val(context, true);
					if (newVal != values[i]) {
						// OK, something changed, so we're going to need a new copy of the list.
						if (result == null) {
							result = ValList.Create();
							for (var j = 0; j < i; j++) result.Add(values[j]);
						}
						result.Add(newVal);
						copied = true;
					}
				}
				if (!copied && result != null) {
					// No change; but we have new results to return, so copy it as-is
					result.Add(values[i]);
				}
			}
			return result ?? this;
		}

		public ValList EvalCopy(TAC.Context context) {
			// Create a copy of this list, evaluating its members as we go.
			// This is used when a list literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			var result = ValList.Create(values.Count);
			for (var i = 0; i < values.Count; i++) {
				result.Add(values[i] == null ? null : values[i].Val(context, true), false);
			}
			return result;
		}

		public override string CodeForm(TAC.Machine vm, int recursionLimit=-1) {
			if (recursionLimit == 0) return "[...]";
			if (recursionLimit > 0 && recursionLimit < 3 && vm != null) {
				string shortName = vm.FindShortName(this);
				if (shortName != null) return shortName;
			}
            if (_workingStringBuilder == null)
                _workingStringBuilder = new StringBuilder();
            else
                _workingStringBuilder.Clear();
            _workingStringBuilder.Append("[");
			for (var i = 0; i < values.Count; i++) {
                Value val = values[i];
                _workingStringBuilder.Append(val == null ? "null" : val.CodeForm(vm, recursionLimit - 1));
                if (i != values.Count - 1)
                    _workingStringBuilder.Append(", ");
			}
            _workingStringBuilder.Append("]");
            return _workingStringBuilder.ToString();
		}

		public override string ToString(TAC.Machine vm) {
			return CodeForm(vm, 3);
		}

		public override bool BoolValue() {
			// A list is considered true if it is nonempty.
			return values != null && values.Count > 0;
		}

		public override bool IsA(Value type, TAC.Machine vm) {
			return type == vm.listType;
		}

		public override int Hash(int recursionDepth=16) {
			//return values.GetHashCode();
			int result = values.Count.GetHashCode();
			if (recursionDepth < 1) return result;
			for (var i = 0; i < values.Count; i++) {
				result ^= values[i].Hash(recursionDepth-1);
			}
			return result;
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			if (!(rhs is ValList)) return 0;
			List<Value> rhl = ((ValList)rhs).values;
			if (rhl == values) return 1;  // (same list)
			int count = values.Count;
			if (count != rhl.Count) return 0;
			if (recursionDepth < 1) return 0.5;		// in too deep
			double result = 1;
			for (var i = 0; i < count; i++) {
				result *= values[i].Equality(rhl[i], recursionDepth-1);
				if (result <= 0) break;
			}
			return result;
		}

		public override bool CanSetElem() { return true; }

		public override void SetElem(Value index, Value value) {
            SetElem(index, value, true);
		}
		public void SetElem(Value index, Value value, bool takeValueRef) {
			var i = index.IntValue();
			if (i < 0) i += values.Count;
			if (i < 0 || i >= values.Count) {
				throw new IndexException("Index Error (list index " + index + " out of range)");
			}
            // Unref existing
            PoolableValue existing = values[i] as PoolableValue;
            if (existing != null)
                existing.Unref();
            // Ref new
            if (takeValueRef)
            {
                PoolableValue poolVal = value as PoolableValue;
                if (poolVal != null)
                    poolVal.Ref();
            }
			values[i] = value;
		}
        public void RemoveAt(int i)
        {
            PoolableValue existing = values[i] as PoolableValue;
            if (existing != null)
                existing.Unref();
            values.RemoveAt(i);
        }
        public Value GetElem(Value index) {
			var i = index.IntValue();
			if (i < 0) i += values.Count;
			if (i < 0 || i >= values.Count) {
				throw new IndexException("Index Error (list index " + index + " out of range)");

			}
			return values[i];
		}
        public Value this[int i]
        {
            get { return values[i]; }
            set {
                // Unref existing
                PoolableValue existing = values[i] as PoolableValue;
                if (existing != null)
                    existing.Unref();
                // Ref new
                PoolableValue poolVal = value as PoolableValue;
                if (poolVal != null)
                    poolVal.Ref();
                values[i] = value;
            }
        }
    }
	
	/// <summary>
	/// ValMap represents a MiniScript map, which under the hood is just a Dictionary
	/// of Value, Value pairs.
	/// </summary>
	public class ValMap : PoolableValue, IEnumerable {
		private readonly Dictionary<Value, Value> map;

		// Assignment override function: return true to cancel (override)
		// the assignment, or false to allow it to happen as normal.
		public delegate bool AssignOverrideFunc(Value key, Value value);
		public AssignOverrideFunc assignOverride;
        [ThreadStatic]
        protected static ValuePool<ValMap> _valuePool;
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }
        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;

		private ValMap(bool usePool) : base(usePool) {
			this.map = new Dictionary<Value, Value>(RValueEqualityComparer.instance);
		}
        protected override void ResetState()
        {
            foreach(var kvp in map)
            {
                PoolableValue poolableKey = kvp.Key as PoolableValue;
                PoolableValue poolableVal = kvp.Value as PoolableValue;
                if (poolableKey != null)
                    poolableKey.Unref();
                if (poolableVal != null)
                    poolableVal.Unref();
            }
            map.Clear();
        }
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValMap>();
            _valuePool.ReturnToPool(this);
            //Console.WriteLine("Returning ValMap");
        }
        public static ValMap Create()
        {
            if (_valuePool == null)
                _valuePool = new ValuePool<ValMap>();
            else
            {
                ValMap valMap = _valuePool.GetInstance();
                if (valMap != null)
                {
                    valMap._refCount = 1;
                    return valMap;
                }
            }
            //Console.WriteLine("Creating ValMap");
            _numInstancesAllocated++;
            return new ValMap(true);
        }
        public override void Ref()
        {
            base.Ref();
            //Console.WriteLine("ValMap Ref ref count " + base._refCount);
        }
        public override void Unref()
        {
            base.Unref();
            //Console.WriteLine("ValMap unref ref count " + base._refCount);
        }

        public override bool BoolValue() {
			// A map is considered true if it is nonempty.
			return map != null && map.Count > 0;
		}

		/// <summary>
		/// Convenience method to check whether the map contains a given string key.
		/// </summary>
		/// <param name="identifier">string key to check for</param>
		/// <returns>true if the map contains that key; false otherwise</returns>
		public bool ContainsKey(string identifier) {
			var idVal = TempValString.Get(identifier);
			bool result = map.ContainsKey(idVal);
			TempValString.Release(idVal);
			return result;
		}
		
		/// <summary>
		/// Convenience method to check whether this map contains a given key
		/// (of arbitrary type).
		/// </summary>
		/// <param name="key">key to check for</param>
		/// <returns>true if the map contains that key; false otherwise</returns>
		public bool ContainsKey(Value key) {
			if (key == null) key = ValNull.instance;
			return map.ContainsKey(key);
		}
		
		/// <summary>
		/// Get the number of entries in this map.
		/// </summary>
		public int Count {
			get { return map.Count; }
		}
		
		/// <summary>
		/// Return the KeyCollection for this map.
		/// </summary>
		public Dictionary<Value, Value>.KeyCollection Keys {
			get { return map.Keys; }
		}

		/// <summary>
		/// Return the ValueCollection for this map.
		/// </summary>
		public Dictionary<Value, Value>.ValueCollection Values {
			get { return map.Values; }
		}
		
		/// <summary>
		/// Accessor to get/set on element of this map by a string key, walking
		/// the __isa chain as needed.  (Note that if you want to avoid that, then
		/// simply look up your value in .map directly.)
		/// </summary>
		/// <param name="identifier">string key to get/set</param>
		/// <returns>value associated with that key</returns>
		public Value this [string identifier] {
			get { 
				var idVal = TempValString.Get(identifier);
				Value result = Lookup(idVal);
				TempValString.Release(idVal);
				return result;
			}
			set {
                //ValString valStr = ValString.Create(identifier);
                PoolableValue poolableValue = value as PoolableValue;
                if (poolableValue != null)
                    poolableValue.Ref();

                ValString idVal = ValString.Create(identifier);
                if(map.TryGetValue(idVal, out Value existing))
                {
                    // Unref the existing
                    PoolableValue valPool = existing as PoolableValue;
                    if (valPool != null)
                        valPool.Unref();
                }
                map[idVal] = value;
            }
		}

		public Value this [Value identifier] {
			get {
                return map[identifier];
			}
			set {
                PoolableValue poolableValue = value as PoolableValue;
                if (poolableValue != null)
                    poolableValue.Ref();

                if(map.TryGetValue(identifier, out Value existing))
                {
                    // Unref the existing
                    PoolableValue valPool = existing as PoolableValue;
                    if (valPool != null)
                        valPool.Unref();
                }
                else
                {
                    // Ref the key
                    PoolableValue keyPool = identifier as PoolableValue;
                    if (keyPool != null)
                        keyPool.Ref();
                }
                map[identifier] = value;
            }
		}
		
		/// <summary>
		/// Look up the given identifier as quickly as possible, without
		/// walking the __isa chain or doing anything fancy.  (This is used
		/// when looking up local variables.)
		/// </summary>
		/// <param name="identifier">identifier to look up</param>
		/// <returns>true if found, false if not</returns>
		public bool TryGetValue(string identifier, out Value value) {
			var idVal = TempValString.Get(identifier);
			bool result = map.TryGetValue(idVal, out value);
			TempValString.Release(idVal);
			return result;
		}
		/// <summary>
		/// Look up the given identifier as quickly as possible, without
		/// walking the __isa chain or doing anything fancy.  (This is used
		/// when looking up local variables.)
		/// </summary>
		/// <param name="identifier">identifier to look up</param>
		/// <returns>true if found, false if not</returns>
		public bool TryGetValue(Value identifier, out Value value) {
			bool result = map.TryGetValue(identifier, out value);
			return result;
		}
		
		/// <summary>
		/// Look up a value in this dictionary, walking the __isa chain to find
		/// it in a parent object if necessary.
		/// </summary>
		/// <param name="key">key to search for</param>
		/// <returns>value associated with that key, or null if not found</returns>
		public Value Lookup(Value key) {
			if (key == null) key = ValNull.instance;
			Value result = null;
			ValMap obj = this;
			while (obj != null) {
				if (obj.map.TryGetValue(key, out result)) return result;
				Value parent;
				if (!obj.map.TryGetValue(ValString.magicIsA, out parent)) break;
				obj = parent as ValMap;
			}
			return null;
		}
		
		/// <summary>
		/// Look up a value in this dictionary, walking the __isa chain to find
		/// it in a parent object if necessary; return both the value found an
		/// (via the output parameter) the map it was found in.
		/// </summary>
		/// <param name="key">key to search for</param>
		/// <returns>value associated with that key, or null if not found</returns>
		public Value Lookup(Value key, out ValMap valueFoundIn) {
			if (key == null) key = ValNull.instance;
			Value result = null;
			ValMap obj = this;
			while (obj != null) {
				if (obj.map.TryGetValue(key, out result)) {
					valueFoundIn = obj;
					return result;
				}
				Value parent;
				if (!obj.map.TryGetValue(ValString.magicIsA, out parent)) break;
				obj = parent as ValMap;
			}
			valueFoundIn = null;
			return null;
		}
		
		public override Value FullEval(TAC.Context context) {
			// Evaluate each of our elements, and if any of those is
			// a variable or temp, then resolve those now.
			foreach (Value k in map.Keys.ToArray()) {	// TODO: something more efficient here.
				Value key = k;		// stupid C#!
				Value value = map[key];
				if (key is ValTemp || key is ValVar) {
					map.Remove(key);
					key = key.Val(context, true);
					map[key] = value;
				}
				if (value is ValTemp || value is ValVar) {
					map[key] = value.Val(context, true);
				}
			}
			return this;
		}

		public ValMap EvalCopy(TAC.Context context) {
			// Create a copy of this map, evaluating its members as we go.
			// This is used when a map literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			var result = ValMap.Create();
			foreach (Value k in map.Keys) {
				Value key = k;		// stupid C#!
				Value value = map[key];
				if (key is ValTemp || key is ValVar) key = key.Val(context, true);
				if (value is ValTemp || value is ValVar) value = value.Val(context, true);
				result[key] = value;
			}
			return result;
		}

		public override string CodeForm(TAC.Machine vm, int recursionLimit=-1) {
			if (recursionLimit == 0) return "{...}";
			if (recursionLimit > 0 && recursionLimit < 3 && vm != null) {
				string shortName = vm.FindShortName(this);
				if (shortName != null) return shortName;
			}
            if (_workingStringBuilder == null)
                _workingStringBuilder = new StringBuilder();
            else
                _workingStringBuilder.Clear();
            _workingStringBuilder.Append("{");
			int i = 0;
			foreach (KeyValuePair<Value, Value> kv in map) {
				int nextRecurLimit = recursionLimit - 1;
				if (kv.Key == ValString.magicIsA)
                    nextRecurLimit = 1;
                _workingStringBuilder.Append(kv.Key.CodeForm(vm, nextRecurLimit));
                _workingStringBuilder.Append(": ");
                _workingStringBuilder.Append(kv.Value == null ? "null" : kv.Value.CodeForm(vm, nextRecurLimit));
                if(++i != map.Count)
                    _workingStringBuilder.Append(", ");
			}
            _workingStringBuilder.Append("}");
            return _workingStringBuilder.ToString();
		}

		public override string ToString(TAC.Machine vm) {
			return CodeForm(vm, 3);
		}

		public override bool IsA(Value type, TAC.Machine vm) {
			// If the given type is the magic 'map' type, then we're definitely
			// one of those.  Otherwise, we have to walk the __isa chain.
			if (type == vm.mapType) return true;
			Value p = null;
			map.TryGetValue(ValString.magicIsA, out p);
			while (p != null) {
				if (p == type) return true;
				if (!(p is ValMap)) return false;
				((ValMap)p).map.TryGetValue(ValString.magicIsA, out p);
			}
			return false;
		}

		public override int Hash(int recursionDepth=16) {
			//return map.GetHashCode();
			int result = map.Count.GetHashCode();
			if (recursionDepth < 0) return result;  // (important to recurse an odd number of times, due to bit flipping)
			foreach (KeyValuePair<Value, Value> kv in map) {
				result ^= kv.Key.Hash(recursionDepth-1);
				if (kv.Value != null) result ^= kv.Value.Hash(recursionDepth-1);
			}
			return result;
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			if (!(rhs is ValMap)) return 0;
			Dictionary<Value, Value> rhm = ((ValMap)rhs).map;
			if (rhm == map) return 1;  // (same map)
			int count = map.Count;
			if (count != rhm.Count) return 0;
			if (recursionDepth < 1) return 0.5;		// in too deep
			double result = 1;
			foreach (KeyValuePair<Value, Value> kv in map) {
				if (!rhm.ContainsKey(kv.Key)) return 0;
				var rhvalue = rhm[kv.Key];
				if (kv.Value == null) {
					if (rhvalue != null) return 0;
					continue;
				}
				result *= kv.Value.Equality(rhvalue, recursionDepth-1);
				if (result <= 0) break;
			}
			return result;
		}

		public override bool CanSetElem() { return true; }

		/// <summary>
		/// Set the value associated with the given key (index).  This is where
		/// we take the opportunity to look for an assignment override function,
		/// and if found, give that a chance to handle it instead.
		/// </summary>
		public override void SetElem(Value index, Value value) {
            SetElem(index, value, true);
		}
		public void SetElem(Value index, Value value, bool takeValueRef) {
            //Console.WriteLine("Map set elem " + index.ToString() + ": " + value.ToString());
            if (takeValueRef)
            {
                // If the value is poolable, ref it
                PoolableValue valPool = value as PoolableValue;
                if(valPool != null)
                    valPool.Ref();
            }
			if (index == null) index = ValNull.instance;
			if (assignOverride == null || !assignOverride(index, value)) {

                //TODO there may be issues where two indexes are different instances
                // but are Equal(). Then we should be careful about unreffing the current
                // instance. Not sure if that normally happens though
                if(map.TryGetValue(index, out Value existing))
                {
                    // Unref what's currently there
                    PoolableValue existingPoolVal = existing as PoolableValue;
                    if(existingPoolVal != null)
                        existingPoolVal.Unref();
                    map[index] = value;
                }
                else
                {
                    // If the index/value is poolable, ref it
                    PoolableValue indexPool = index as PoolableValue;
                    if(indexPool != null)
                        indexPool.Ref();
                    map[index] = value;
                }
			}
		}
		public void SetElem(string index, Value value, bool takeValueRef) {
            //TODO unref current string key if present
            ValString keyStr = ValString.Create(index);
            SetElem(keyStr, value, takeValueRef);
            keyStr.Unref();
		}

		/// <summary>
		/// Get the indicated key/value pair as another map containing "key" and "value".
		/// (This is used when iterating over a map with "for".)
		/// </summary>
		/// <param name="index">0-based index of key/value pair to get.</param>
		/// <returns>new map containing "key" and "value" with the requested key/value pair</returns>
		public ValMap GetKeyValuePair(int index) {
			Dictionary<Value, Value>.KeyCollection keys = map.Keys;
			if (index < 0 || index >= keys.Count) {
				throw new IndexException("index " + index + " out of range for map");
			}
			Value key = keys.ElementAt<Value>(index);   // (TODO: consider more efficient methods here)
			var result = ValMap.Create();
			result.map[keyStr] = (key is ValNull ? null : key);
			result.map[valStr] = map[key];
			return result;
		}
        IEnumerator IEnumerable.GetEnumerator()
        {
            return map.GetEnumerator();
        }
        public Dictionary<Value, Value>.Enumerator GetEnumerator()
        {
            return map.GetEnumerator();
        }
        public bool Remove(Value keyVal)
        {
            // Pull the current key/value so that we can unref it
            if(map.TryGetValue(keyVal, out Value existing))
            {
                PoolableValue keyPool = keyVal as PoolableValue;
                PoolableValue valPool = existing as PoolableValue;
                if (keyPool != null)
                    keyPool.Unref();
                if (valPool != null)
                    valPool.Unref();
                map.Remove(keyVal);
                return true;
            }
            return false;
        }

        static ValString keyStr = ValString.Create("key", false);
		static ValString valStr = ValString.Create("value", false);

	}
	
	/// <summary>
	/// Function: our internal representation of a MiniScript function.  This includes
	/// its parameters and its code.  (It does not include a name -- functions don't 
	/// actually HAVE names; instead there are named variables whose value may happen 
	/// to be a function.)
	/// </summary>
	public class Function {
        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;
		/// <summary>
		/// Param: helper class representing a function parameter.
		/// </summary>
		public class Param {
			public string name;
			public Value defaultValue;

			public Param(string name, Value defaultValue) {
				this.name = name;
				this.defaultValue = defaultValue;
			}
		}
		
		// Function parameters
		public List<Param> parameters;
		
		// Function code (compiled down to TAC form)
		public List<TAC.Line> code;

		public Function(List<TAC.Line> code) {
			this.code = code;
			parameters = new List<Param>();
		}

		public string ToString(TAC.Machine vm) {
            if (_workingStringBuilder == null)
                _workingStringBuilder = new StringBuilder();
            else
                _workingStringBuilder.Clear();
			_workingStringBuilder.Append("FUNCTION(");			
			for (var i=0; i < parameters.Count(); i++) {
				if (i > 0) _workingStringBuilder.Append(", ");
				_workingStringBuilder.Append(parameters[i].name);
				if (parameters[i].defaultValue != null) _workingStringBuilder.Append("=" + parameters[i].defaultValue.CodeForm(vm));
			}
			_workingStringBuilder.Append(")");
			return _workingStringBuilder.ToString();
		}
	}
	
	/// <summary>
	/// ValFunction: a Value that is, in fact, a Function.
	/// </summary>
	public class ValFunction : Value {
		public Function function;
		public ValMap outerVars;	// local variables where the function was defined (usually, the module)

		public ValFunction(Function function) {
			this.function = function;
		}

		public override string ToString(TAC.Machine vm) {
			return function.ToString(vm);
		}

		public override bool BoolValue() {
			// A function value is ALWAYS considered true.
			return true;
		}

		public override bool IsA(Value type, TAC.Machine vm) {
			return type == vm.functionType;
		}

		public override int Hash(int recursionDepth=16) {
			return function.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			// Two Function values are equal only if they refer to the exact same function
			if (!(rhs is ValFunction)) return 0;
			var other = (ValFunction)rhs;
			return function == other.function ? 1 : 0;
		}

	}

	public class ValTemp : Value {
		public int tempNum;

		public ValTemp(int tempNum) {
			this.tempNum = tempNum;
		}

		public override Value Val(TAC.Context context, bool takeRef) {
			return context.GetTemp(tempNum);
		}

		public override Value Val(TAC.Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			return context.GetTemp(tempNum);
		}

		public override string ToString(TAC.Machine vm) {
			return "_" + tempNum.ToString(CultureInfo.InvariantCulture);
		}

		public override int Hash(int recursionDepth=16) {
			return tempNum.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return rhs is ValTemp && ((ValTemp)rhs).tempNum == tempNum ? 1 : 0;
		}

	}

	public class ValVar : Value {
		public string identifier;
		public bool noInvoke;	// reflects use of "@" (address-of) operator

		public ValVar(string identifier) {
			this.identifier = identifier;
		}

		public override Value Val(TAC.Context context, bool takeRef) {
			return context.GetVar(identifier);
		}

		public override Value Val(TAC.Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			return context.GetVar(identifier);
		}

		public override string ToString(TAC.Machine vm) {
			if (noInvoke) return "@" + identifier;
			return identifier;
		}

		public override int Hash(int recursionDepth=16) {
			return identifier.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return rhs is ValVar && ((ValVar)rhs).identifier == identifier ? 1 : 0;
		}

		// Special name for the implicit result variable we assign to on expression statements:
		public static ValVar implicitResult = new ValVar("_");
	}

	public class ValSeqElem : Value {
		public Value sequence;
		public Value index;
		public bool noInvoke;	// reflects use of "@" (address-of) operator

		public ValSeqElem(Value sequence, Value index) {
			this.sequence = sequence;
			this.index = index;
		}

		/// <summary>
		/// Look up the given identifier in the given sequence, walking the type chain
		/// until we either find it, or fail.
		/// </summary>
		/// <param name="sequence">Sequence (object) to look in.</param>
		/// <param name="identifier">Identifier to look for.</param>
		/// <param name="context">Context.</param>
		public static Value Resolve(Value sequence, string identifier, TAC.Context context, out ValMap valueFoundIn) {
			var includeMapType = true;
			valueFoundIn = null;
			int loopsLeft = 1000;		// (max __isa chain depth)
			while (sequence != null) {
				if (sequence is ValTemp || sequence is ValVar) sequence = sequence.Val(context, false);
				if (sequence is ValMap) {
					// If the map contains this identifier, return its value.
					Value result = null;
					var idVal = TempValString.Get(identifier);
					bool found = ((ValMap)sequence).TryGetValue(idVal, out result);
					TempValString.Release(idVal);
					if (found) {
						valueFoundIn = (ValMap)sequence;
						return result;
					}
					
					// Otherwise, if we have an __isa, try that next.
					if (loopsLeft < 0) return null;		// (unless we've hit the loop limit)
					if (!((ValMap)sequence).TryGetValue(ValString.magicIsA, out sequence)) {
						// ...and if we don't have an __isa, try the generic map type if allowed
						if (!includeMapType) throw new KeyException(identifier);
						sequence = context.vm.mapType ?? Intrinsics.MapType();
						includeMapType = false;
					}
				} else if (sequence is ValList) {
					sequence = context.vm.listType ?? Intrinsics.ListType();
					includeMapType = false;
				} else if (sequence is ValString) {
					sequence = context.vm.stringType ?? Intrinsics.StringType();
					includeMapType = false;
				} else if (sequence is ValNumber) {
					sequence = context.vm.numberType ?? Intrinsics.NumberType();
					includeMapType = false;
				} else if (sequence is ValFunction) {
					sequence = context.vm.functionType ?? Intrinsics.FunctionType();
					includeMapType = false;
				} else {
					throw new TypeException("Type Error (while attempting to look up " + identifier + ")");
				}
				loopsLeft--;
			}
			return null;
		}

		public override Value Val(TAC.Context context, bool takeRef) {
			ValMap ignored;
			return Val(context, out ignored);
		}
		
		public override Value Val(TAC.Context context, out ValMap valueFoundIn) {
			valueFoundIn = null;
			Value idxVal = index == null ? null : index.Val(context, false);
			if (idxVal is ValString) return Resolve(sequence, ((ValString)idxVal).value, context, out valueFoundIn);
			// Ok, we're searching for something that's not a string;
			// this can only be done in maps and lists (and lists, only with a numeric index).
			Value baseVal = sequence.Val(context, false);
			if (baseVal is ValMap) {
				Value result = ((ValMap)baseVal).Lookup(idxVal, out valueFoundIn);
				if (valueFoundIn == null) throw new KeyException(idxVal.CodeForm(context.vm, 1));
				return result;
			} else if (baseVal is ValList && idxVal is ValNumber) {
				return ((ValList)baseVal).GetElem(idxVal);
			} else if (baseVal is ValString && idxVal is ValNumber) {
				return ((ValString)baseVal).GetElem(idxVal);
			}
				
			throw new TypeException("Type Exception: can't index into this type");
		}

		public override string ToString(TAC.Machine vm) {
			return string.Format("{0}{1}[{2}]", noInvoke ? "@" : "", sequence, index);
		}

		public override int Hash(int recursionDepth=16) {
			return sequence.Hash(recursionDepth-1) ^ index.Hash(recursionDepth-1);
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			return rhs is ValSeqElem && ((ValSeqElem)rhs).sequence == sequence
				&& ((ValSeqElem)rhs).index == index ? 1 : 0;
		}

	}

	public class RValueEqualityComparer : IEqualityComparer<Value> {
		public bool Equals(Value val1, Value val2) {
			return val1.Equality(val2) > 0;
		}

		public int GetHashCode(Value val) {
			return val.Hash();
		}

		static RValueEqualityComparer _instance = null;
		public static RValueEqualityComparer instance {
			get {
				if (_instance == null) _instance = new RValueEqualityComparer();
				return _instance;
			}
		}
	}
}

