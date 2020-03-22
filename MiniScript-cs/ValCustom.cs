using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    public abstract class ValCustom : Value
    {
        public override int GetBaseMiniscriptType()
        {
            return MiniscriptTypeInts.ValCustomTypeInt;
        }
        public abstract ValMap GetTypeFunctionMap();
        public virtual Value Lookup(Value key) { return null; }
        public virtual Value APlusB(Value rhs, int rhsType, Context context)
        {
            return null;
        }
        public virtual Value AMinusB(Value rhs, int rhsType, Context context)
        {
            return null;
        }
        public virtual Value ATimesB(Value rhs, int rhsType, Context context)
        {
            return null;
        }
        public virtual Value ADividedByB(Value rhs, int rhsType, Context context)
        {
            return null;
        }
    }
}
