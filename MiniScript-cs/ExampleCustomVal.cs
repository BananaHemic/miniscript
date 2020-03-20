using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    public class ExampleCustomVal : ValCustom
    {
        public float NumA { get; private set; }
        public string StrB { get; private set; }
        private static readonly string NumAValueName = "numA";
        private static readonly string StringBValueName = "strB";

        private static bool _hasStaticInit = false;
        private static ValMap _typeMap;

        public ExampleCustomVal(float numA, string strB)
        {
            //MiniCompat.Log("Made custom!");
            NumA = numA;
            StrB = strB;
        }
		public override Value Lookup(Value key) {
            //TODO this might need to be removed entirely
            return null;
		}

        public override ValMap GetTypeFunctionMap()
        {
            if (!_hasStaticInit)
                InitializeIntrinsics();
            return _typeMap;
        }

        public override double Equality(Value rhs, int recursionDepth = 16)
        {
            ExampleCustomVal rhsVal = rhs as ExampleCustomVal;
            if (rhsVal == null)
                return 0;

            if (NumA == rhsVal.NumA
                && StrB == rhsVal.StrB)
                return 1;
            return 0;
        }

        public override int Hash(int recursionDepth = 16)
        {
            int hash = NumA.GetHashCode();
            if(StrB != null)
                hash ^= StrB.GetHashCode();
            return hash;
        }

        public override string ToString(Machine vm)
        {
            return StrB;
        }

        public static void InitializeIntrinsics()
        {
            if (_hasStaticInit)
                return;
            _hasStaticInit = true;
            Intrinsic intrinsic;
            // Load the constructor
            intrinsic = Intrinsic.Create("ExampleCustom");
            intrinsic.AddParam(NumAValueName, 0.0);
            intrinsic.AddParam(StringBValueName, "");
            intrinsic.code = (context, partialResult) =>
            {

                ValNumber numA = context.GetVar(NumAValueName) as ValNumber;
                ValString strB = context.GetVar(StringBValueName) as ValString;

                ExampleCustomVal customVal = new ExampleCustomVal(
                    numA != null ? (float)numA.value : 0,
                    strB != null ? strB.value : "");

                return new Intrinsic.Result(customVal);
            };
            Intrinsic getNumAIntrinsic = Intrinsic.Create("GetNumA");
            getNumAIntrinsic.code = (context, partialResult) =>
            {
                ExampleCustomVal self = context.GetVar(ValString.selfStr) as ExampleCustomVal;
                if (self == null)
                    return Intrinsic.Result.Null;
                return new Intrinsic.Result(ValNumber.Create(self.NumA));
            };
            Intrinsic getStrBIntrinsic = Intrinsic.Create("GetStrB");
            getStrBIntrinsic.code = (context, partialResult) =>
            {
                ExampleCustomVal self = context.GetVar(ValString.selfStr) as ExampleCustomVal;
                if (self == null)
                    return Intrinsic.Result.Null;
                return new Intrinsic.Result(ValString.Create(self.StrB));
            };
            // Create a map with the functions for this type
            _typeMap = ValMap.Create();
            _typeMap[NumAValueName] = getNumAIntrinsic.GetFunc();
            _typeMap[StringBValueName] = getStrBIntrinsic.GetFunc();
        }
    }
}
