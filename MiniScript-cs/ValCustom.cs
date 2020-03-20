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
        public abstract Value Lookup(Value key);
        public virtual Value APlusB(Value rhs, int rhsType)
        {
            return null;
        }
        public virtual Value AMinusB(Value opB, int opBTypeInt)
        {
            return null;
        }
        public virtual Value ATimesB(Value opB, int opBTypeInt)
        {
            return null;
        }
        public virtual Value ADividedByB(Value opB, int opBTypeInt)
        {
            return null;
        }
    }
}
