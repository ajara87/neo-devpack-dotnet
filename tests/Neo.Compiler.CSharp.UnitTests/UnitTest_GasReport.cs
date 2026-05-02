// Copyright (C) 2015-2026 The Neo Project.
//
// UnitTest_GasReport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract.Testing;
using System;
using System.IO;

namespace Neo.Compiler.CSharp.UnitTests
{
    [TestClass]
    public class UnitTest_GasReport
    {
        private static string CaptureReport(SmartContract.NefFile nef, SmartContract.Manifest.ContractManifest manifest)
        {
            var writer = new StringWriter();
            GasReporter.Print(nef, manifest, writer);
            return writer.ToString();
        }

        [TestMethod]
        public void Test_GasReport_ContainsContractName()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            Assert.IsTrue(output.Contains("Contract:"), "Report must contain 'Contract:' header");
        }

        [TestMethod]
        public void Test_GasReport_ContainsStaticHeader()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            // The word "Static" must appear in the report header so readers understand
            // this is a compile-time analysis, not a runtime measurement.
            Assert.IsTrue(output.Contains("Static Bytecode Report"), "Report header must read 'Static Bytecode Report' to make its nature explicit");
        }

        [TestMethod]
        public void Test_GasReport_ContainsScriptSize()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            Assert.IsTrue(output.Contains("Script size:"), "Report must contain script size");
            // Script must be non-zero
            Assert.IsTrue(output.Contains("bytes"), "Script size must include 'bytes' unit");
        }

        [TestMethod]
        public void Test_GasReport_ContainsInstructionCount()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            Assert.IsTrue(output.Contains("Instruction count:"), "Report must contain instruction count");
        }

        [TestMethod]
        public void Test_GasReport_InstructionCount_IsNumericOrNA()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            // Extract the value after "Instruction count:"
            int labelIndex = output.IndexOf("Instruction count:", StringComparison.Ordinal);
            Assert.IsTrue(labelIndex >= 0);
            string afterLabel = output[(labelIndex + "Instruction count:".Length)..].TrimStart();
            string value = afterLabel.Split('\n')[0].Trim();
            bool isNumeric = int.TryParse(value, out int count) && count > 0;
            bool isNA = value == "N/A";
            Assert.IsTrue(isNumeric || isNA, $"Instruction count must be a positive integer or N/A, got: '{value}'");
        }

        [TestMethod]
        public void Test_GasReport_ContainsAbiMethods()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            Assert.IsTrue(output.Contains("ABI methods:"), "Report must list ABI method count");
        }

        [TestMethod]
        public void Test_GasReport_ContainsGasEstimationDisclaimer()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            Assert.IsTrue(output.Contains("Gas estimation:"), "Report must contain gas estimation section");
            Assert.IsTrue(output.Contains("cannot be estimated exactly"), "Report must include the disclaimer");
            Assert.IsTrue(output.Contains("Engine.GasConsumed"), "Report must mention Engine.GasConsumed");
        }

        [TestMethod]
        public void Test_GasReport_NoGasValuesInvented()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            // The report must never contain the GAS token symbol or a "X GAS" pattern,
            // which would imply a fabricated runtime cost estimate.
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(output, @"\d+(\.\d+)?\s*GAS"), "Report must not contain any invented GAS cost values");
        }

        [TestMethod]
        public void Test_GasReport_ContainsMethodTable()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            // NEP-17 standard methods should appear
            Assert.IsTrue(output.Contains("transfer"), "Report must list 'transfer' method");
            Assert.IsTrue(output.Contains("balanceOf"), "Report must list 'balanceOf' method");
            Assert.IsTrue(output.Contains("symbol"), "Report must list 'symbol' method");
        }

        [TestMethod]
        public void Test_GasReport_SafeFlag()
        {
            var output = CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            // The method table header must contain the "Safe" column label.
            // We look for it surrounded by whitespace as produced by the fixed-width format
            // so we do not accidentally match substrings in method names or the disclaimer.
            Assert.IsTrue(output.Contains("Safe     Return"), "Report must include the Safe column header");
            // At least one method must be flagged safe=yes (NEP-17 has several safe methods)
            Assert.IsTrue(output.Contains("yes      "), "Report must show at least one safe method");
        }

        [TestMethod]
        public void Test_GasReport_NullNef_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => GasReporter.Print(null!, Contract_NEP17.Manifest, Console.Out));
        }

        [TestMethod]
        public void Test_GasReport_NullManifest_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => GasReporter.Print(Contract_NEP17.Nef, null!, Console.Out));
        }

        [TestMethod]
        public void Test_GasReport_NullWriter_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => GasReporter.Print(Contract_NEP17.Nef, Contract_NEP17.Manifest, null!));
        }

        [TestMethod]
        public void Test_GasReport_ScriptSizeMatchesNef()
        {
            var nef = Contract_NEP17.Nef;
            var manifest = Contract_NEP17.Manifest;
            int expectedSize = nef.Script.Length;

            var output = CaptureReport(nef, manifest);

            Assert.IsTrue(output.Contains($"{expectedSize} bytes"), $"Report must show correct script size: {expectedSize} bytes");
        }

        [TestMethod]
        public void Test_GasReport_EventsSection_WhenPresent()
        {
            var output = CaptureReport(Contract_Event.Nef, Contract_Event.Manifest);
            Assert.IsTrue(output.Contains("Events:"), "Report must contain events count");
        }

        [TestMethod]
        public void Test_GasReport_DoesNotWriteToStdErr()
        {
            // Generating a report for a valid contract must produce no stderr output.
            var stderrCapture = new StringWriter();
            var originalErr = Console.Error;
            try
            {
                Console.SetError(stderrCapture);
                CaptureReport(Contract_NEP17.Nef, Contract_NEP17.Manifest);
            }
            finally
            {
                Console.SetError(originalErr);
            }
            Assert.AreEqual(string.Empty, stderrCapture.ToString(), "GasReporter.Print must not write anything to stderr for a valid contract");
        }

        [TestMethod]
        public void Test_GasReport_SafeMethodCount_MatchesManifest()
        {
            var manifest = Contract_NEP17.Manifest;
            int expectedSafe = 0;
            foreach (var m in manifest.Abi.Methods)
                if (m.Safe) expectedSafe++;

            var output = CaptureReport(Contract_NEP17.Nef, manifest);

            // Extract the value after "Safe methods:"
            int idx = output.IndexOf("Safe methods:", StringComparison.Ordinal);
            Assert.IsTrue(idx >= 0);
            string afterLabel = output[(idx + "Safe methods:".Length)..].TrimStart();
            string value = afterLabel.Split('\n')[0].Trim();
            Assert.IsTrue(int.TryParse(value, out int reported), $"Safe methods count must be an integer, got: '{value}'");
            Assert.AreEqual(expectedSafe, reported, "Reported safe method count must match the manifest ABI");
        }
    }
}
