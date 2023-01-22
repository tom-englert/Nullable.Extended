using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nullable.Extended.Analyzer.Test
{
    [TestClass]
    public class OptionsTests
    {
        [TestMethod]
        public void ParseAllSetTest()
        {
            var config = @"
    <LogFile>LogPath</LogFile>
    <DisableSuppressions>true</DisableSuppressions>
    <MaxSteps>42</MaxSteps>
    <Dummy>Something</Dummy>
";
            var target = Options.Deserialize(config);

            Assert.AreEqual(true, target.DisableSuppressions);
            Assert.AreEqual(42, target.MaxSteps);
        }

        [TestMethod]
        public void ParseEmptyTest()
        {
            var config = @"
    <Dummy>Something</Dummy>
";
            var target = Options.Deserialize(config);

            Assert.AreEqual(false, target.DisableSuppressions);
            Assert.AreEqual(null, target.MaxSteps);
        }

        [TestMethod]
        public void ParseBoolAnyCaseTest()
        {
            var config = @"
    <DisableSuppressions>TruE</DisableSuppressions>
    <MaxSteps>42</MaxSteps>
";
            var target = Options.Deserialize(config);

            Assert.AreEqual(true, target.DisableSuppressions);
            Assert.AreEqual(42, target.MaxSteps);
        }


    }
}
