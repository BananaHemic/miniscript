using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    /// <summary>
    /// A completely useless Value. Used only as a reference for how
    /// to integrate your own types, and to test that custom types
    /// work properly
    /// </summary>
    public class ExampleCustomVal : ValCustom
    {
        public float NumA { get; private set; }
        public string StrB { get; private set; }
        private static readonly string NumAValueName = "numA";
        private static readonly string StringBValueName = "strB";

        private static bool _hasStaticInit = false;
        private static ValMap _typeMap;

        public ExampleCustomVal(float numA, string strB) : base(false)
        {
            //MiniCompat.Log("Made custom!");
            NumA = numA;
            StrB = strB;
        }
        public override Value APlusB(Value other, int otherType, Context context, bool isSelfLhs)
        {
            ExampleCustomVal val = other as ExampleCustomVal;
            if (val == null)
                return null;

            return new ExampleCustomVal(NumA + val.NumA, StrB + val.StrB);
        }
        public override Value AMinusB(Value other, int otherType, Context context, bool isSelfLhs)
        {
            ExampleCustomVal val = other as ExampleCustomVal;
            if (val == null)
                return null;

            return new ExampleCustomVal(NumA - val.NumA, "???");
        }
        public override Value ATimesB(Value other, int otherType, Context context, bool isSelfLhs)
        {
            if(otherType == MiniscriptTypeInts.ValNumberTypeInt)
            {
                ValNumber valNum = other as ValNumber;
                if (valNum == null)
                    return null;
                return new ExampleCustomVal(NumA * (float)valNum.value, "???");
            }

            ExampleCustomVal val = other as ExampleCustomVal;
            if (val == null)
                return null;

            return new ExampleCustomVal(NumA * val.NumA, "???");
        }
        public override Value ADividedByB(Value other, int otherType, Context context, bool isSelfLhs)
        {
            ExampleCustomVal val = other as ExampleCustomVal;
            if (val == null)
                return null;

            return new ExampleCustomVal(NumA / val.NumA, "???");
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

        public ExampleCustomVal Inverse()
        {
            return new ExampleCustomVal(-NumA, StrB);
        }

        public static void InitializeIntrinsics()
        {
            if (_hasStaticInit)
                return;
            _hasStaticInit = true;
            // Load the constructor
            Intrinsic ctor = Intrinsic.Create("ExampleCustom");
            ctor.AddParam(NumAValueName, 0.0);
            ctor.AddParam(StringBValueName, "");
            ctor.code = (context, partialResult) =>
            {

                ValNumber numA = context.GetVar(NumAValueName) as ValNumber;
                ValString strB = context.GetVar(StringBValueName) as ValString;

                ExampleCustomVal customVal = new ExampleCustomVal(
                    numA != null ? (float)numA.value : 0,
                    strB != null ? strB.value : "");

                return new Intrinsic.Result(customVal);
            };
            Intrinsic getNumAIntrinsic = Intrinsic.Create("GetNumA", false);
            getNumAIntrinsic.code = (context, partialResult) =>
            {
                ExampleCustomVal self = context.GetVar(ValString.selfStr) as ExampleCustomVal;
                if (self == null)
                    return Intrinsic.Result.Null;
                return new Intrinsic.Result(ValNumber.Create(self.NumA));
            };
            Intrinsic getStrBIntrinsic = Intrinsic.Create("GetStrB", false);
            getStrBIntrinsic.code = (context, partialResult) =>
            {
                ExampleCustomVal self = context.GetVar(ValString.selfStr) as ExampleCustomVal;
                if (self == null)
                    return Intrinsic.Result.Null;
                return new Intrinsic.Result(ValString.Create(self.StrB));
            };
            Intrinsic invIntrinsic = Intrinsic.Create("inv", false);
            invIntrinsic.code = (context, partialResult) =>
            {
                ExampleCustomVal self = context.GetVar(ValString.selfStr) as ExampleCustomVal;
                if (self == null)
                    return Intrinsic.Result.Null;
                return new Intrinsic.Result(self.Inverse());
            };
            // Create a map with the functions for this type
            _typeMap = ValMap.Create();
            _typeMap[NumAValueName] = getNumAIntrinsic.GetFunc();
            _typeMap[StringBValueName] = getStrBIntrinsic.GetFunc();
            _typeMap["inv"] = invIntrinsic.GetFunc();
        }

        protected override void ResetState()
        {
        }
        protected override void ReturnToPool()
        {
        }
    }
}
