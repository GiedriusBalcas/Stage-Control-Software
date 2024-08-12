using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NUnit_tests
{
    [TestFixture]
    public class Paiter_Tests
    {
        [Test]
        public void RunOpenTKConsoleApp_Test()
        {
            // Path to your console application
            string appPath = @"C:\Users\giedr\OneDrive\Desktop\importsnt\Csharp\Standa Stage Control Environment\standa_controller_software\ConsoleApplication_For_Tests\bin\Debug\net8.0\ConsoleApplication_For_Tests.exe";

            // Start the process
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();

                // Optional: Capture the output
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                // Optional: Perform assertions on the output
                Assert.IsTrue(process.ExitCode == 0, "Console application exited with a non-zero exit code.");
                Assert.IsEmpty(error, $"Console application threw an error: {error}");

                // Further assertions based on output
                Assert.IsTrue(output.Contains("ExpectedOutput"), "The output did not contain the expected string.");
            }
        }
    }
}
