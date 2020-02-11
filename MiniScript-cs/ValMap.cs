﻿using System;
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

        private static int _num;
        private int _id;

		private ValMap(bool usePool) : base(usePool) {
			this.map = new Dictionary<Value, Value>(RValueEqualityComparer.instance);
            _id = _num++;
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
                    valMap._id = _num++;
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
		
		public override Value FullEval(Context context) {
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

		public ValMap EvalCopy(Context context) {
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

		public override string CodeForm(Machine vm, int recursionLimit=-1) {
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

		public override string ToString(Machine vm) {
			return CodeForm(vm, 3);
		}

		public override bool IsA(Value type, Machine vm) {
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
		public void SetElem(Value index, Value value, bool takeValueRef, bool takeIndexRef=true) {
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
                    if (takeIndexRef)
                    {
                        // If the index/value is poolable, ref it
                        PoolableValue indexPool = index as PoolableValue;
                        if(indexPool != null)
                            indexPool.Ref();
                    }
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
}
