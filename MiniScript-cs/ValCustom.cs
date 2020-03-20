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
    }
}
