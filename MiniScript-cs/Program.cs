using System;
using Miniscript;
using System.IO;
using System.Collections.Generic;

class MainClass {

	static void Print(string s) {
		Console.WriteLine(s);
	}

	static void ListErrors(Script script) {
		if (script.errors == null) {
			Print("No errors.");
			return;
		}
		foreach (Error err in script.errors) {
			Print(string.Format("{0} on line {1}: {2}",
				err.type, err.lineNum, err.description));
		}

	}

	static void Test(List<string> sourceLines, int sourceLineNum,
					 List<string> expectedOutput, int outputLineNum) {
		if (expectedOutput == null) expectedOutput = new List<string>();
        //		Console.WriteLine("TEST (LINE {0}):", sourceLineNum);
        //		Console.WriteLine(string.Join("\n", sourceLines));
        //		Console.WriteLine("EXPECTING (LINE {0}):", outputLineNum);
        //		Console.WriteLine(string.Join("\n", expectedOutput));

        Interpreter miniscript = new Interpreter(sourceLines);

        // Uncomment to test with separate parsing / running
        //Parser parser = new Parser();
        //try {
        //    parser.Parse(string.Join("\n", sourceLines.ToArray()));
        //} catch (Miniscript.CompilerException mse) {
        //    Console.WriteLine("Error parsing " + mse);
        //    return;
        //}
        //Interpreter miniscript = new Interpreter(parser);

        // Get the current number of PoolableVars in use across the system
#if MINISCRIPT_DEBUG
        long numValNumAllocated = ValNumber.NumInstancesInUse;
        long numValStrAllocated = ValString.NumInstancesInUse;
        long numValMapAllocated = ValMap.NumInstancesInUse;
        long numValListAllocated = ValList.NumInstancesInUse;
        ValNumber._instances.Clear();
#endif

		List<string> actualOutput = new List<string>();
        //miniscript.standardOutput = (string s) => { actualOutput.Add(s); Console.WriteLine(s); };
        miniscript.standardOutput = (string s) => actualOutput.Add(s);
        miniscript.errorOutput = miniscript.standardOutput;
		miniscript.implicitOutput = miniscript.standardOutput;
		miniscript.RunUntilDone(6000, false);

//		Console.WriteLine("ACTUAL OUTPUT:");
//		Console.WriteLine(string.Join("\n", actualOutput));

		int minLen = expectedOutput.Count < actualOutput.Count ? expectedOutput.Count : actualOutput.Count;
		for (int i = 0; i < minLen; i++) {
			if (actualOutput[i] != expectedOutput[i]) {
				Print(string.Format("TEST FAILED AT LINE {0}\n  EXPECTED: {1}\n    ACTUAL: {2}",
					outputLineNum + i, expectedOutput[i], actualOutput[i]));
			}
		}
		if (expectedOutput.Count > actualOutput.Count) {
			Print(string.Format("TEST FAILED: MISSING OUTPUT AT LINE {0}", outputLineNum + actualOutput.Count));
			for (int i = actualOutput.Count; i < expectedOutput.Count; i++) {
				Print("  MISSING: " + expectedOutput[i]);
			}
		} else if (actualOutput.Count > expectedOutput.Count) {
			Print(string.Format("TEST FAILED: EXTRA OUTPUT AT LINE {0}", outputLineNum + expectedOutput.Count));
			for (int i = expectedOutput.Count; i < actualOutput.Count; i++) {
				Print("  EXTRA: " + actualOutput[i]);
			}
		}

        miniscript.Dispose();
#if MINISCRIPT_DEBUG
        long finalNumValNumAllocated = ValNumber.NumInstancesInUse;
        long finalNumValStrAllocated = ValString.NumInstancesInUse;
        long finalNumValMapAllocated = ValMap.NumInstancesInUse;
        long finalNumValListAllocated = ValList.NumInstancesInUse;

        if (numValNumAllocated != finalNumValNumAllocated)
        {
            Print(string.Format("Leaking ValNumber, was {0} now {1}", numValNumAllocated, finalNumValNumAllocated));
            Console.Write("Unreturned: ");
            foreach(var i in ValNumber._instances)
                Console.Write(i + ", ");
        }
        if (numValStrAllocated != finalNumValStrAllocated)
            Print(string.Format("Leaking ValString, was {0} now {1}", numValStrAllocated, finalNumValStrAllocated));
        if (numValMapAllocated != finalNumValMapAllocated)
            Print(string.Format("Leaking ValMap, was {0} now {1}", numValMapAllocated, finalNumValMapAllocated));
        if (numValListAllocated != finalNumValListAllocated)
            Print(string.Format("Leaking ValList, was {0} now {1}", numValListAllocated, finalNumValListAllocated));
#endif
    }

	static void RunTestSuite(string path) {
		StreamReader file = new StreamReader(path);
		if (file == null) {
			Print("Unable to read: " + path);
			return;
		}

		List<string> sourceLines = null;
		List<string> expectedOutput = null;
		int testLineNum = 0;
		int outputLineNum = 0;

		string line = file.ReadLine();
		int lineNum = 1;
		while (line != null) {
			if (line.StartsWith("====")) {
				if (sourceLines != null) Test(sourceLines, testLineNum, expectedOutput, outputLineNum);
				sourceLines = null;
				expectedOutput = null;
			} else if (line.StartsWith("----")) {
				expectedOutput = new List<string>();
				outputLineNum = lineNum + 1;
			} else if (expectedOutput != null) {
				expectedOutput.Add(line);
			} else {
				if (sourceLines == null) {
					sourceLines = new List<string>();
					testLineNum = lineNum;
				}
				sourceLines.Add(line);
			}

			line = file.ReadLine();
			lineNum++;
		}
		if (sourceLines != null) Test(sourceLines, testLineNum, expectedOutput, outputLineNum);
		Print("\nIntegration tests complete.\n");
	}

	static void RunFile(string path, bool dumpTAC=false) {
		StreamReader file = new StreamReader(path);
		if (file == null) {
			Print("Unable to read: " + path);
			return;
		}

		List<string> sourceLines = new List<string>();
		while (!file.EndOfStream) sourceLines.Add(file.ReadLine());

		Interpreter miniscript = new Interpreter(sourceLines);
		miniscript.standardOutput = (string s) => Print(s);
		miniscript.implicitOutput = miniscript.standardOutput;
		miniscript.Compile();

		if (dumpTAC) {
			miniscript.vm.DumpTopContext();
		}
		
		while (!miniscript.done) {
			miniscript.RunUntilDone();
		}

	}

	public static void Main(string[] args) {
		
		Miniscript.HostInfo.name = "Test harness";
		
		//Print("Miniscript test harness.\n");

		//Print("Running unit tests.\n");
		//UnitTest.Run();

		Print("Running test suite.\n");
        Intrinsics.InitIfNeeded();
        Intrinsics.MapType();
        Intrinsics.StringType();
        Intrinsics.NumberType();
        Intrinsics.FunctionType();
        Intrinsics.ListType();
        ExampleCustomVal.InitializeIntrinsics();
        for(int i = 1; i < Intrinsic.all.Count; i++)
            Intrinsic.all[i].GetFunc();
        //Intrinsic.GetByName("slice").GetFunc();
        //Intrinsic.GetByName("print").GetFunc();
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        Console.WriteLine("--------------");
        //RunTestSuite("../../../TestSuite.txt");
        RunTestSuite("../../../TestSuite_min.txt");
        //RunTestSuite("../../../TestSuite_split.txt");
        stopwatch.Stop();
        // Current time for full test: 256ms
        Print("Elapsed execution time: " + stopwatch.ElapsedMilliseconds + "ms");

        Print("\n");

		const string quickTestFilePath = "../../../QuickTest.mscp";

		if (File.Exists(quickTestFilePath)) {
			Print("Running quick test.\n");
			RunFile(quickTestFilePath, true);
		} else {
			Print("Quick test not found, skipping...\n");
		}


		if (args.Length > 0) {
			RunFile(args[0]);
			return;
		}
		
		Interpreter repl = new Interpreter();
		repl.implicitOutput = repl.standardOutput;

		while (true) {
			Console.Write(repl.NeedMoreInput() ? ">>> " : "> ");
			string inp = Console.ReadLine();
			if (inp == null) break;
			repl.REPL(inp);
		}
	}
}
