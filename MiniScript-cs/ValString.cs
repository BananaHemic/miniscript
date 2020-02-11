using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
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

            switch (val)
            {
                case " ":
                    return spaceStr;
                case "to":
                    return toStr;
                case "from":
                    return fromStr;
                case "__isa":
                    return magicIsA;
                case "seq":
                    return seqStr;
                case "self":
                    return selfStr;
                case "super":
                    return superStr;
                case "len":
                    return lenStr;
            }

            Console.WriteLine("Alloc str \"" + val + "\"");
            if (val == "r")
            { }

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
            if (!base._poolable)
                return;
            if (value == "r")
            { }
                //Console.WriteLine("Str " + value + " ref, ref count #" + _refCount);
                base.Ref();
        }
        public override void Unref()
        {
            if (!base._poolable)
                return;
            if (base._refCount == 0)
            {
                Console.WriteLine("Extra unref for: " + value);
            }
            if (value == "r")
            { }
            base.Unref();
            //if (value == "zzz")
                //Console.WriteLine("Str " + value + " unref, ref count #" + _refCount);
        }
        protected override void ResetState()
        {
            Console.WriteLine("Str \"" + value + "\" back in pool");
            //value = null;
        }
        protected override void ReturnToPool()
        {
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValString>();
            _valuePool.ReturnToPool(this);
        }

        public override string ToString(Machine vm) {
			return value;
		}

		public override string CodeForm(Machine vm, int recursionLimit=-1) {
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

		public override bool IsA(Value type, Machine vm) {
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
		public static ValString spaceStr = new ValString(" ", false);
		public static ValString fromStr = new ValString("from", false);
		public static ValString toStr = new ValString("to", false);
		public static ValString seqStr = new ValString("seq", false);
		public static ValString superStr = new ValString("super", false);
		public static ValString lenStr = new ValString("len", false);
		
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
	
}
