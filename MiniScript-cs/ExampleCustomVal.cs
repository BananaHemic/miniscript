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
        const string NumAValueName = "numA";
        const string StringBValueName = "strB";
        const string InverseValueName = "inv";
        private static bool _hasStaticInit = false;

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
        public override bool Resolve(string identifier, Context ctx, out Value ret)
        {
            switch (identifier)
            {
                case NumAValueName:
                    ret = ValNumber.Create(NumA);
                    return true;
                case StringBValueName:
                    ret = ValString.Create(StrB);
                    return true;
                case InverseValueName:
                    ret = Inverse();
                    return true;
            }
            ret = null;
            return false;
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
        }
        protected override void ResetState()
        {
        }
        protected override void ReturnToPool()
        {
        }
    }
}
