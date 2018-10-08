using Microsoft.Azure.SignalR.Benchmark.DataModule;
using System;
using Xunit;

namespace Microsoft.Azure.SignalR.Benchmark.Tests
{
    public class TestBenchmarkConfigurationModule
    {
        [Fact]
        public void TestParse()
        {
            var input = @"---
ModuleName: myModuleName

Types:
- P1
- P2

Pipeline:
 # first step
-
  - Type: P1
    Method: echo
    Parameter:
      Total: 1000
      Idle: 200
  - Type: P2
    Method: Create
    Parameter:
      Total: 999
      Idle: 200
# second step
- 
  - Type: P1
    Method: Echo
    Parameter:
      Total: 1000
      Idle: 200
  - Type: P2
    Method: Create
    Parameter:
      Total: 2000
      Idle: 333
";
            var benchmarkConfiguration = new BenchmarkConfigurationModule();
            benchmarkConfiguration.Parse(input);

            // Basic information
            Assert.True(benchmarkConfiguration.ModuleName == "myModuleName", $"moduleName != 'myModuleName'");
            Assert.True(benchmarkConfiguration.Types[0] == "P1", $"type1 != 'P1', '{benchmarkConfiguration.Types[0]} instead'");
            Assert.True(benchmarkConfiguration.Types[1] == "P2", $"type2 != 'P2'");

            // Step 1
            Assert.True(benchmarkConfiguration.Pipeline[0][0].Type == "P1", $"type != P1");
            Assert.True(benchmarkConfiguration.Pipeline[0][0].Method == "echo", $"method != Echo");
            Assert.True(benchmarkConfiguration.Pipeline[0][1].Type == "P2", $"type != P2");
            Assert.True(benchmarkConfiguration.Pipeline[0][1].Method == "Create", $"method != Create");
            Assert.True(benchmarkConfiguration.Pipeline[0][1].IntegerDictionary["Total"] == 999, $"total != 999");

            // Step 2
            Assert.True(benchmarkConfiguration.Pipeline[1][0].Type == "P1", $"type != P1");
            Assert.True(benchmarkConfiguration.Pipeline[1][0].Method == "Echo", $"method != Echo");
            Assert.True(benchmarkConfiguration.Pipeline[1][1].Type == "P2", $"type != P2");
            Assert.True(benchmarkConfiguration.Pipeline[1][1].Method == "Create", $"method != Create");
            Assert.True(benchmarkConfiguration.Pipeline[1][1].IntegerDictionary["Idle"] == 333, $"Idle != 333");

            // Test serialization and deserialization
            var jsonList = benchmarkConfiguration.Pipeline[0][1].Serialize();
            Assert.True(jsonList.Count > 0, $"{jsonList.Count}");
            var step = new EchoSampleStep();
            step.Deserialize(jsonList);
            Assert.True(step.GetTotalConnetion() == 999, $"Error serializing and deserializing parameters. total != 999, {step.GetTotalConnetion()} instead");
            Assert.True(step.GetIdleConnetion() == 200, $"Error serializing and deserializing parameters. idle != 200, {step.GetIdleConnetion()} instead");
            

        }
    }
}
 