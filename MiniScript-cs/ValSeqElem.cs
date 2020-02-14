﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	public class ValSeqElem : PoolableValue {
        [ThreadStatic]
        protected static ValuePool<ValSeqElem> _valuePool;
        [ThreadStatic]
        protected static uint _numInstancesAllocated = 0;
        public static long NumInstancesInUse { get { return _numInstancesAllocated - (_valuePool == null ? 0 : _valuePool.Count); } }

		public Value sequence;
		public Value index;
		public bool noInvoke;	// reflects use of "@" (address-of) operator

        public static ValSeqElem Create(Value sequence, Value index)
        {
            if (_valuePool == null)
                _valuePool = new ValuePool<ValSeqElem>();
            else
            {
                ValSeqElem val = _valuePool.GetInstance();
                if(val != null)
                {
                    val.sequence = sequence;
                    val.index = index;
                    val._refCount = 1;
                    return val;
                }
            }
            _numInstancesAllocated++;
            return new ValSeqElem(sequence, index);
        }

        private ValSeqElem(Value sequence, Value index) : base(true){
			this.sequence = sequence;
			this.index = index;
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
            if(sequence != null)
                sequence.Unref();
            sequence = null;
            if(index != null)
                index.Unref();
            index = null;
        }
        protected override void ReturnToPool()
        {
        }

        /// <summary>
        /// Look up the given identifier in the given sequence, walking the type chain
        /// until we either find it, or fail.
        /// </summary>
        /// <param name="sequence">Sequence (object) to look in.</param>
        /// <param name="identifier">Identifier to look for.</param>
        /// <param name="context">Context.</param>
        public static Value Resolve(Value sequence, string identifier, Context context, out ValMap valueFoundIn) {
			var includeMapType = true;
			valueFoundIn = null;
			int loopsLeft = 1000;		// (max __isa chain depth)
			while (sequence != null) {
				if (sequence is ValTemp || sequence is ValVar) sequence = sequence.Val(context, false);
				if (sequence is ValMap) {
					// If the map contains this identifier, return its value.
					Value result = null;
					var idVal = ValString.Create(identifier);
					bool found = ((ValMap)sequence).TryGetValue(idVal, out result);
                    idVal.Unref();
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

		public override Value Val(Context context, bool takeRef) {
			ValMap ignored;
			Value v = Val(context, out ignored);
            if (v != null)
                v.Ref();
            return v;
		}
		
		public override Value Val(Context context, out ValMap valueFoundIn) {
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

		public override string ToString(Machine vm) {
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
}
