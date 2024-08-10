using text_parser_library;

namespace NUnit_tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Variable_Test()
        {
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "variable-test-script.txt");
            string fileContent = File.ReadAllText(filePath);

            var parser = new TextInterpreterWrapper();
            parser.ReadInput(fileContent);
            var library = parser.Visitor.GetCurrentLibrary();
            library.TryGetVariable("c", out var value);

            TestContext.WriteLine($"The value of 'c' is: {value}");

            int intValue = Convert.ToInt32(value);

            // Assert that the value of 'c' is equal to 3
            Assert.AreEqual(8, intValue);
        }

        [Test]
        public void Gloabl_Variable_Test()
        {
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "global-variable-test-script.txt");
            string fileContent = File.ReadAllText(filePath);

            var parserWrapper = new TextInterpreterWrapper();
            parserWrapper.DefinitionLibrary.AddVariable("PI", (float)Math.PI);

            try
            {
                parserWrapper.ReadInput(fileContent);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine(parserWrapper.State.Message);
                TestContext.WriteLine(ex.Message);
                throw ex;
            }

            var library = parserWrapper.Visitor.GetCurrentLibrary();
            library.TryGetVariable("a", out var value);

            TestContext.WriteLine($"The value of 'a' is: {value}");

            double doubleValue = Convert.ToDouble(value);

            // Assert that the value of 'c' is equal to 3
            Assert.AreEqual(3 * (float)Math.PI, doubleValue);
        }

        [Test]
        public void FunctionDefinitionAndCallTest()
        {
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "function-test-script.txt");
            string fileContent = File.ReadAllText(filePath);

            var parserWrapper = new TextInterpreterWrapper();
            try
            {
                parserWrapper.ReadInput(fileContent);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine(parserWrapper.State.Message);
                TestContext.WriteLine(ex.Message);
                throw ex;
            }
            var library = parserWrapper.Visitor.GetCurrentLibrary();
            library.TryGetVariable("c", out var value);

            TestContext.WriteLine($"The value of 'c' is: {value}");

            int intValue = Convert.ToInt32(value);

            // Assert that the value of 'c' is equal to 3
            Assert.AreEqual(10, intValue);
        }


        [Test]
        public void OutsideFunctionDefinitionAndCallTest()
        {
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "write-function-test-script.txt");
            string fileContent = File.ReadAllText(filePath);

            var parserWrapper = new TextInterpreterWrapper();
            var writeFunc = new ConsoleLogFunction();
            parserWrapper.DefinitionLibrary.AddFunction("write", writeFunc);


            try
            {
                parserWrapper.ReadInput(fileContent);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine(parserWrapper.State.Message);
                TestContext.WriteLine(ex.Message);
                throw ex;
            }

            //TestContext.WriteLine($"{writeFunc.Message}");
            Assert.Pass();
        }



        [Test]
        public void ReadFileTest()
        {
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "read-function-test-script.txt");
            string fileContent = File.ReadAllText(filePath);

            var parserWrapper = new TextInterpreterWrapper();
            var writeFunc = new ConsoleLogFunction();
            parserWrapper.DefinitionLibrary.AddFunction("write", writeFunc);

            try
            {
                parserWrapper.ReadInput(fileContent);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine(parserWrapper.State.Message);
                TestContext.WriteLine(ex.Message);
                throw ex;
            }

            Assert.Pass();
        }

        [Test]
        public void MoveATest()
        {
            string filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_scripts", "moveA-function-test-script.txt");
            string fileContent = File.ReadAllText(filePath);

            var parserWrapper = new TextInterpreterWrapper();
            var writeFunc = new ConsoleLogFunction();
            parserWrapper.DefinitionLibrary.AddFunction("write", writeFunc);

            try
            {
                parserWrapper.ReadInput(fileContent);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine(parserWrapper.Visitor.State.Message);
                TestContext.WriteLine(ex.Message);
            }

            Assert.Pass();
        }



    }
    
}