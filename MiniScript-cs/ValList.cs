using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
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

        private static int _num;
        public int _id;

        private ValList(int capacity, bool poolable) : base(poolable) {
            _id = _num++;
            values = new List<Value>(capacity);
		}
        public static ValList Create(int capacity=0)
        {
            //Console.WriteLine("ValList create cap = " + capacity + " ID " + _num);
            if (_num == 1)
            { }
            if (_valuePool == null)
                _valuePool = new ValuePool<ValList>();
            else
            {
                ValList existing = _valuePool.GetInstance();
                if(existing != null)
                {
                    existing._refCount = 1;
                    existing.EnsureCapacity(capacity);
                    existing._id = _num++;
                    return existing;
                }
            }

            _numInstancesAllocated++;
            return new ValList(capacity, true);
        }
        public override void Ref()
        {
            if(_id == 1)
            { }
            base.Ref();
        }
        public override void Unref()
        {
            if(_id == 1)
            { }
            if(base._refCount == 0)
                Console.WriteLine("ValList #" + _id + " double unref");
            base.Unref();
        }
        protected override void ResetState()
        {
            for(int i = 0; i < values.Count;i++)
                values[i].Unref();
            //Console.WriteLine("ValList #" + _id + " back in pool");
            values.Clear();
        }
        public void Add(Value value, bool takeRef=true)
        {
            ValString str = value as ValString;
            if (str != null && str._id == 109)
            { }
            if (_id == 10)
                { }
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
            if (_id == 10)
                { }

            // Ref all the input variables
            // we need to do this first, otherwise, there's
            // weird issues where we unref into the pool, then ref
            for (int i = 0; i < recvValues.Count; i++)
                recvValues[i]?.Ref();
            // Unref the values we have
            for (int i = 0; i < values.Count; i++)
                values[i]?.Unref();
            values.Clear();
            // Copy them over
            for (int i = 0; i < recvValues.Count; i++)
                values.Add(recvValues[i]);
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
            //Console.WriteLine("ValList #" + _id + " returned");
        }
        public void Insert(int idx, Value value)
        {
            if (_id == 10)
                { }
            ValString str = value as ValString;
            if (str != null && str._id == 109)
            { }
            PoolableValue poolableValue = value as PoolableValue;
            if (poolableValue != null)
                poolableValue.Ref();
            values.Insert(idx, value);
        }
        public override Value FullEval(Context context) {
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

		public ValList EvalCopy(Context context) {
			// Create a copy of this list, evaluating its members as we go.
			// This is used when a list literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			var result = ValList.Create(values.Count);
			for (var i = 0; i < values.Count; i++) {
                // Sometimes Val is a ValTemp that returns a value that should be reffed
                // so we Val without Refing, then Ref during Add
				result.Add(values[i] == null ? null : values[i].Val(context, false), true);
			}
			return result;
		}

		public override string CodeForm(Machine vm, int recursionLimit=-1) {
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

		public override string ToString(Machine vm) {
			return CodeForm(vm, 3);
		}

		public override bool BoolValue() {
			// A list is considered true if it is nonempty.
			return values != null && values.Count > 0;
		}

		public override bool IsA(Value type, Machine vm) {
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
            ValString str = value as ValString;
            if (str != null && str._id == 109)
            { }
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
            ValString str = value as ValString;
            if (str != null && str._id == 109)
            { }
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
}
