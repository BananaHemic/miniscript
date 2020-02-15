using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// ValMap represents a MiniScript map, which under the hood is just a Dictionary
	/// of Value, Value pairs.
	/// </summary>
	public class ValMap : PoolableValue {

		// Assignment override function: return true to cancel (override)
		// the assignment, or false to allow it to happen as normal.
		public delegate bool AssignOverrideFunc(Value key, Value value);
		public AssignOverrideFunc assignOverride;
        [ThreadStatic]
        protected static ValuePool<ValMap> _valuePool;
        [ThreadStatic]
        private static StringBuilder _workingStringBuilder;
#if MINISCRIPT_DEBUG
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }
        private static int _num;
        public int _id;
#endif

		private readonly Dictionary<Value, Value> map;
        private readonly List<Value> _mapKeys = new List<Value>();
        private readonly List<Value> _allKeys = new List<Value>();
        private readonly List<Value> _allValues = new List<Value>();
        // Common values in the map, which we keep here for perf
        private Value selfVal;
        private Value isaVal;
        private Value eventsVal;
        private Value xVal;
        private Value yVal;
        private Value zVal;
        private Value wVal;

		private ValMap(bool usePool) : base(usePool) {
			this.map = new Dictionary<Value, Value>(RValueEqualityComparer.instance);
#if MINISCRIPT_DEBUG
            _id = _num++;
#endif
		}
        public static ValMap Create()
        {
            //Console.WriteLine("Creating ValMap ID " + _num);
            if (_valuePool == null)
                _valuePool = new ValuePool<ValMap>();
            else
            {
                ValMap valMap = _valuePool.GetInstance();
                if (valMap != null)
                {
                    valMap._refCount = 1;
#if MINISCRIPT_DEBUG
                    valMap._id = _num++;
#endif
                    return valMap;
                }
            }
#if MINISCRIPT_DEBUG
            _numInstancesAllocated++;
#endif
            return new ValMap(true);
        }
#if MINISCRIPT_DEBUG
        public override void Ref()
        {
            base.Ref();
            //Console.WriteLine("ValMap Ref ref count " + base._refCount);
        }
        public override void Unref()
        {
            if (_refCount == 0)
                Console.WriteLine("Extra unref for map ID #" + _id);
            base.Unref();

            // Handle de-ref when a map self-references.
            // it only works when there's a single level of
            // self reference, and it's fairly expensive, such that
            // I'd rather just leak
            //int numBackReferences = 0;
            //foreach(var kvp in map)
            //{
            //    ValMap valMap = kvp.Value as ValMap;
            //    if(valMap != null)
            //    {
            //        if (valMap.ContainsValue(this))
            //            numBackReferences++;
            //    }
            //    else
            //    {
            //        ValList valList = kvp.Value as ValList;
            //        if (valList != null)
            //        { }
            //    }
            //}
            //if(numBackReferences == _refCount)
            //{
            //    Console.WriteLine("Unreffing, we contain all our own references!");
            //    for(int i = 0; i < numBackReferences; i++)
            //        base.Unref();
            //}

            //Console.WriteLine("ValMap unref ref count " + base._refCount);
        }
#endif
        protected override void ResetState()
        {
            foreach(var kvp in map)
            {
                kvp.Key?.Unref();
                kvp.Value?.Unref();
            }
            map.Clear();
            _mapKeys.Clear();
            _allValues.Clear();
            _allKeys.Clear();

            selfVal?.Unref();
            selfVal = null;
            isaVal?.Unref();
            isaVal = null;
            eventsVal?.Unref();
            eventsVal = null;
            xVal?.Unref();
            xVal = null;
            yVal?.Unref();
            yVal = null;
            zVal?.Unref();
            zVal = null;
            wVal?.Unref();
            wVal = null;
        }
        protected override void ReturnToPool()
        {
            //Console.WriteLine("Returning ValMap ID " + _id);
            if (!base._poolable)
                return;
            if (_valuePool == null)
                _valuePool = new ValuePool<ValMap>();
            _valuePool.ReturnToPool(this);
        }

		/// <summary>
		/// Get the number of entries in this map.
		/// </summary>
		public int Count {
			get {
                // Count the values that we have stored
                // as built-in
                int numBuiltIn = 0;
                if (selfVal != null) numBuiltIn++;
                if (isaVal != null) numBuiltIn++;
                if (eventsVal != null) numBuiltIn++;
                if (xVal != null) numBuiltIn++;
                if (yVal != null) numBuiltIn++;
                if (zVal != null) numBuiltIn++;
                if (wVal != null) numBuiltIn++;

                return numBuiltIn + map.Count;
            }
		}
		
		/// <summary>
		/// Return the KeyCollection for this map.
		/// </summary>
		public List<Value> Keys {
            get
            {
                _allKeys.Clear();
                if (selfVal != null)
                    _allKeys.Add(ValString.selfStr);
                if (isaVal != null)
                    _allKeys.Add(ValString.magicIsA);
                if (eventsVal != null)
                    _allKeys.Add(ValString.eventsStr);
                if (xVal != null)
                    _allKeys.Add(ValString.xStr);
                if (yVal != null)
                    _allKeys.Add(ValString.yStr);
                if (zVal != null)
                    _allKeys.Add(ValString.zStr);
                if (wVal != null)
                    _allKeys.Add(ValString.wStr);
                _allKeys.AddRange(_mapKeys);
                return _allKeys;
            }
		}

		/// <summary>
		/// Return a list of Values for this map.
        /// NB: this list stays owned by the ValMap
		/// </summary>
		public List<Value> Values {
			get {
                _allValues.Clear();
                if (selfVal != null)
                    _allValues.Add(selfVal);
                if (isaVal != null)
                    _allValues.Add(isaVal);
                if (eventsVal != null)
                    _allValues.Add(eventsVal);
                if (xVal != null)
                    _allValues.Add(xVal);
                if (yVal != null)
                    _allValues.Add(yVal);
                if (zVal != null)
                    _allValues.Add(zVal);
                if (wVal != null)
                    _allValues.Add(wVal);
                foreach (var val in map.Values)
                    _allValues.Add(val);
                return _allValues;
            }
		}
		
        private bool TryGetInternalBuiltIn(string identifier, out Value value)
        {
            switch (identifier)
            {
                case "self":
                    value = selfVal;
                    return true;
                case "__isa":
                    value = isaVal;
                    return true;
                case "__events":
                    value = eventsVal;
                    return true;
                case "x":
                    value = xVal;
                    return true;
                case "y":
                    value = yVal;
                    return true;
                case "z":
                    value = zVal;
                    return true;
                case "w":
                    value = wVal;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }
        private bool TrySetInternalBuiltIn(string identifier, Value value)
        {
            switch (identifier)
            {
                case "self":
                    selfVal?.Unref();
                    selfVal = value;
                    return true;
                case "__isa":
                    isaVal?.Unref();
                    isaVal = value;
                    return true;
                case "__events":
                    eventsVal?.Unref();
                    eventsVal = value;
                    return true;
                case "x":
                    xVal?.Unref();
                    xVal = value;
                    return true;
                case "y":
                    yVal?.Unref();
                    yVal = value;
                    return true;
                case "z":
                    zVal?.Unref();
                    zVal = value;
                    return true;
                case "w":
                    wVal?.Unref();
                    wVal = value;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

		/// <summary>
		/// Set the value associated with the given key (index).  This is where
		/// we take the opportunity to look for an assignment override function,
		/// and if found, give that a chance to handle it instead.
		/// </summary>
		public override void SetElem(Value index, Value value) {
            SetElem(index, value, true);
		}
		public void SetElem(Value index, Value value, bool takeValueRef, bool takeIndexRef=true) {
            ValNumber newNum = value as ValNumber;
            //Console.WriteLine("Map set elem " + index.ToString() + ": " + value.ToString());
            if (takeValueRef)
                value?.Ref();
            if (takeIndexRef)
                index?.Ref();
			if (index == null) index = ValNull.instance;

			if (assignOverride == null || !assignOverride(index, value)) {

                // Check against common entries first, for perf
                ValString indexStr = index as ValString;
                if(indexStr != null)
                {
                    // We want to replicate the behavior of a map, so to
                    // preserve the way that you can set a key to null, we
                    // simply store a ValNull here, and pull out a ValNull
                    // later but just return a null
                    Value builtInVal = value ?? ValNull.instance;
                    if (TrySetInternalBuiltIn(indexStr.value, builtInVal))
                        return;
                }

                if(map.TryGetValue(index, out Value existing))
                {
                    // Unref the value that's currently there
                    existing.Unref();
                    // Try to get the key that's there and unref it
                    Value existingKey = RemoveBySwap(_mapKeys, index);
                    map.Remove(existingKey);
                    if (existingKey != null)
                        existingKey.Unref();
                }
                _mapKeys.Add(index);
                map[index] = value;
			}
		}
        public bool Remove(Value keyVal)
        {
            // Check against common entries first, for perf
            ValString indexStr = keyVal as ValString;
            Value existing;
            if(indexStr != null)
            {
                // We return true only if we have an existing value
                if(TryGetInternalBuiltIn(indexStr.value, out existing))
                {
                    if(existing != null)
                    {
                        if (TrySetInternalBuiltIn(indexStr.value, null))
                            return true;
                    }
                    return false;
                }
            }
            // Pull the current key/value so that we can unref it
            if(map.TryGetValue(keyVal, out existing))
            {
                existing.Unref();
                // Try to get the key that's there and unref it
                Value existingKey = RemoveBySwap(_mapKeys, keyVal);
                if (existingKey != null)
                    existingKey.Unref();
                map.Remove(keyVal);
                return true;
            }
            return false;
        }
		public void SetElem(string index, Value value, bool takeValueRef) {
            ValString keyStr = ValString.Create(index);
            SetElem(keyStr, value, takeValueRef);
            keyStr.Unref();
		}
        // O(n)
        private Value RemoveBySwap(List<Value> list, Value newKey)
        {
            for(int i = 0; i < _mapKeys.Count; i++)
            {
                Value key = _mapKeys[i];
                if (key.Equality(newKey) == 1.0)
                {
                    _mapKeys[i] = _mapKeys[_mapKeys.Count - 1];
                    _mapKeys.RemoveAt(_mapKeys.Count - 1);
                    return key;
                }
            }
            return null;
        }
        /// <summary>
        /// Accessor to get/set on element of this map by a string key, walking
        /// the __isa chain as needed.  (Note that if you want to avoid that, then
        /// simply look up your value in .map directly.)
        /// </summary>
        /// <param name="identifier">string key to get/set</param>
        /// <returns>value associated with that key</returns>
        public Value this [string identifier] {
            //TODO I think we might unecessarily be walking up the _isa chain here
			get { 
				var idVal = ValString.Create(identifier);
                Value result = Lookup(idVal);
                idVal.Unref();
                return result;
			}
			set {
                SetElem(identifier, value, true);
            }
		}

		public Value this [Value identifier] {
			get {
                if(TryGetValue(identifier, out Value ret))
                    return ret;
                return null;
			}
			set {
                SetElem(identifier, value, true);
            }
		}
		
		/// <summary>
		/// Convenience method to check whether the map contains a given string key.
		/// </summary>
		/// <param name="identifier">string key to check for</param>
		/// <returns>true if the map contains that key; false otherwise</returns>
		public bool ContainsKey(string identifier) {
            if(TryGetInternalBuiltIn(identifier, out Value existing))
                return existing != null;
			var idVal = ValString.Create(identifier);
			bool result = map.ContainsKey(idVal);
            idVal.Unref();
			return result;
		}
		
		/// <summary>
		/// Convenience method to check whether this map contains a given key
		/// (of arbitrary type).
		/// </summary>
		/// <param name="key">key to check for</param>
		/// <returns>true if the map contains that key; false otherwise</returns>
		public bool ContainsKey(Value key) {
			if (key == null)
                key = ValNull.instance;
            else
            {
                ValString valStr = key as ValString;
                if (valStr != null && TryGetInternalBuiltIn(valStr.value, out Value existing))
                    return existing != null;
            }
			return map.ContainsKey(key);
		}
		
		/// <summary>
		/// Look up the given identifier as quickly as possible, without
		/// walking the __isa chain or doing anything fancy.  (This is used
		/// when looking up local variables.)
		/// </summary>
		/// <param name="identifier">identifier to look up</param>
		/// <returns>true if found, false if not</returns>
		public bool TryGetValue(string identifier, out Value value) {
			var idVal = ValString.Create(identifier);
			bool result = TryGetValue(idVal, out value);
            idVal.Unref();
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
            ValString valStr = identifier as ValString;
            if (valStr != null && TryGetInternalBuiltIn(valStr.value, out value))
            {
                if (value == null)
                    return false; // Not found
                if (value is ValNull)// 
                    value = null;
                return true;
            }
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
				if (obj.TryGetValue(key, out result)) return result;
				Value parent;
				if (!obj.TryGetValue(ValString.magicIsA, out parent)) break;
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
				if (obj.TryGetValue(key, out result)) {
					valueFoundIn = obj;
					return result;
				}
				Value parent;
				if (!obj.TryGetValue(ValString.magicIsA, out parent)) break;
				obj = parent as ValMap;
			}
			valueFoundIn = null;
			return null;
		}
		
		public override Value FullEval(Context context) {
            // Evaluate each of our elements, and if any of those is
            // a variable or temp, then resolve those now.
            var keys = Keys;
            var vals = Values;
			for(int i = 0; i < keys.Count; i++) {
                Value key = keys[i];
                Value value = vals[i];
				if (key is ValTemp || key is ValVar) {
					Remove(key);
					key = key.Val(context, true);
                    SetElem(key, value);
				}
				if (value is ValTemp || value is ValVar) {
                    SetElem(key, value.Val(context, true));
				}
			}
			return this;
		}

		public ValMap EvalCopy(Context context) {
			// Create a copy of this map, evaluating its members as we go.
			// This is used when a map literal appears in the source, to
			// ensure that each time that code executes, we get a new, distinct
			// mutable object, rather than the same object multiple times.
			var result = ValMap.Create();
            var keys = Keys;
            var values = Values;
			for(int i = 0; i < keys.Count; i++) {
				Value key = keys[i];
                Value value = values[i];
				if (key is ValTemp || key is ValVar)
                    key = key.Val(context, false);
				if (value is ValTemp || value is ValVar)
                    value = value.Val(context, false);
                result.SetElem(key, value, true, true);
			}
			return result;
		}

		public override string CodeForm(Machine vm, int recursionLimit=-1) {
			if (recursionLimit == 0) return "{...}";
			if (recursionLimit > 0 && recursionLimit < 3 && vm != null) {
				string shortName = vm.FindShortName(this);
				if (shortName != null) return shortName;
			}
            //TODO this will break with recursion!
            if (_workingStringBuilder == null)
                _workingStringBuilder = new StringBuilder();
            else
                _workingStringBuilder.Clear();
            _workingStringBuilder.Append("{");
			int i = 0;
            var keys = Keys;
            var values = Values;
            for(int j = 0; j < keys.Count; j++)
            {
                Value key = keys[j];
                Value val = values[j];
				int nextRecurLimit = recursionLimit - 1;
				if (key == ValString.magicIsA)
                    nextRecurLimit = 1;
                _workingStringBuilder.Append(key.CodeForm(vm, nextRecurLimit));
                _workingStringBuilder.Append(": ");
                _workingStringBuilder.Append(val == null ? "null" : val.CodeForm(vm, nextRecurLimit));
                if(++i != map.Count)
                    _workingStringBuilder.Append(", ");
			}
            _workingStringBuilder.Append("}");
            return _workingStringBuilder.ToString();
		}

		public override string ToString(Machine vm) {
			return CodeForm(vm, 3);
		}

		public override bool IsA(Value type, Machine vm) {
			// If the given type is the magic 'map' type, then we're definitely
			// one of those.  Otherwise, we have to walk the __isa chain.
			if (type == vm.mapType) return true;
			Value p = isaVal;
			while (p != null) {
				if (p == type) return true;
				if (!(p is ValMap)) return false;
				((ValMap)p).TryGetValue(ValString.magicIsA, out p);
			}
			return false;
		}

        public override bool BoolValue() {
			// A map is considered true if it is nonempty.
			return Count > 0;
		}

		public override int Hash(int recursionDepth=16) {
			int result = Count.GetHashCode();
			if (recursionDepth < 0) return result;  // (important to recurse an odd number of times, due to bit flipping)
            var keys = Keys;
            var vals = Values;
			for(int i = 0; i < keys.Count; i++) {
				result ^= keys[i].Hash(recursionDepth-1);
                Value val = vals[i];
				if (val != null)
                    result ^= val.Hash(recursionDepth-1);
			}
			return result;
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			if (!(rhs is ValMap)) return 0;
            ValMap rhm = rhs as ValMap;
			if (rhm == this) return 1;  // (same map)
			if (Count != rhm.Count) return 0;
			if (recursionDepth < 1) return 0.5;		// in too deep
			double result = 1;
            var ourKeys = Keys;
            var ourVals = Values;
            var theirKeys = rhm.Keys;
            var theirVals = rhm.Values;

            for(int i = 0; i < ourKeys.Count; i++) {

                if (ourKeys[i] != theirKeys[i])
                    return 0;
                Value ourVal = ourVals[i];
                Value theirVal = theirVals[i];
                if (ourVal == null && theirVal != null)
                    return 0;
                if (ourVal == null && theirVal == null)
                    continue;
				result *= ourVal.Equality(theirVal, recursionDepth-1);
				if (result <= 0) break;
			}
			return result;
		}

		/// <summary>
		/// Get the indicated key/value pair as another map containing "key" and "value".
		/// (This is used when iterating over a map with "for".)
		/// </summary>
		/// <param name="index">0-based index of key/value pair to get.</param>
		/// <returns>new map containing "key" and "value" with the requested key/value pair</returns>
		public ValMap GetKeyValuePair(int index) {
            var keys = Keys;
			if (index < 0 || index >= keys.Count) {
				throw new IndexException("index " + index + " out of range for map");
			}
            var val = Values[index];
            var key = keys[index];
			var result = ValMap.Create();
            result.SetElem(keyStr, (key is ValNull) ? null : key, true, true);
            result.SetElem(valStr, val, true, true);
			return result;
		}

		public override bool CanSetElem() { return true; }

        static ValString keyStr = ValString.Create("key", false);
		static ValString valStr = ValString.Create("value", false);
	}
}
