using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
	/// <summary>
	/// ValFunction: a Value that is, in fact, a Function.
	/// </summary>
	public class ValFunction : Value {
		public Function function;
		public ValMap outerVars;	// local variables where the function was defined (usually, the module)

		public ValFunction(Function function) {
			this.function = function;
		}

        public override void Unref()
        {
            if (function != null)
                function.Dispose();
            function = null;
        }

        public override string ToString(Machine vm) {
			return function.ToString(vm);
		}

		public override bool BoolValue() {
			// A function value is ALWAYS considered true.
			return true;
		}

		public override bool IsA(Value type, Machine vm) {
			return type == vm.functionType;
		}

		public override int Hash(int recursionDepth=16) {
			return function.GetHashCode();
		}

		public override double Equality(Value rhs, int recursionDepth=16) {
			// Two Function values are equal only if they refer to the exact same function
			if (!(rhs is ValFunction)) return 0;
			var other = (ValFunction)rhs;
			return function == other.function ? 1 : 0;
		}

	}
}
