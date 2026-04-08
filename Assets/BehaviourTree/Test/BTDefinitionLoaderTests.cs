using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Shibafu.BehaviourTree;
using Shibafu.BehaviourTree.Serialization;

namespace Shibafu.BehaviourTree.Tests
{
    public class BTDefinitionLoaderTests
    {
        private const string SampleJson = @"
{
  ""formatVersion"": 1,
  ""root"": {
    ""type"": ""selector"",
    ""name"": ""Root"",
    ""children"": [
      {
        ""type"": ""sequence"",
        ""children"": [
          {
            ""type"": ""condition"",
            ""data"": { ""mode"": ""intCompare"", ""key"": ""hp"", ""op"": ""gt"", ""value"": 0 }
          },
          {
            ""type"": ""action"",
            ""data"": { ""handler"": ""succeed"" }
          }
        ]
      },
      {
        ""type"": ""action"",
        ""data"": { ""handler"": ""fail"" }
      }
    ]
  }
}";

        [Test]
        public void LoadTree_SelectorPicksFirstBranch_WhenSequenceSucceeds()
        {
            var ctx = BTDefinitionLoadContext.CreateWithBuiltIns();
            ctx.RegisterActionHandler("succeed", _ => BTStatus.Success);
            ctx.RegisterActionHandler("fail", _ => BTStatus.Failure);

            var bt = BTDefinitionLoader.LoadTree(SampleJson, ctx);
            var blackboard = new BTContext();
            blackboard.Set("hp", 10);
            Assert.AreEqual(BTStatus.Success, bt.Tick(blackboard));
        }

        [Test]
        public void LoadTree_SelectorFallsThrough_WhenConditionFails()
        {
            var ctx = BTDefinitionLoadContext.CreateWithBuiltIns();
            ctx.RegisterActionHandler("succeed", _ => BTStatus.Success);
            ctx.RegisterActionHandler("fail", _ => BTStatus.Failure);

            var bt = BTDefinitionLoader.LoadTree(SampleJson, ctx);
            var blackboard = new BTContext();
            blackboard.Set("hp", 0);
            Assert.AreEqual(BTStatus.Failure, bt.Tick(blackboard));
        }

        [Test]
        public void Wait_ZeroSeconds_SucceedsOnFirstTick()
        {
            const string json = @"{
  ""formatVersion"": 1,
  ""root"": { ""type"": ""wait"", ""data"": { ""seconds"": 0 } }
}";
            var bt = BTDefinitionLoader.LoadTree(json, BTDefinitionLoadContext.CreateWithBuiltIns());
            Assert.AreEqual(BTStatus.Success, bt.Tick(new BTContext()));
        }

        [Test]
        public void NormalizeUniqueIds_DedupesDuplicateIds_WithDeterministicSuffix()
        {
            const string json = @"{
  ""formatVersion"": 1,
  ""root"": { ""type"": ""sequence"", ""id"": ""rootid"", ""children"": [
    { ""type"": ""wait"", ""id"": ""same"", ""data"": { ""seconds"": 0 } },
    { ""type"": ""wait"", ""id"": ""same"", ""data"": { ""seconds"": 0 } }
  ]}
}";
            var doc = BTDefinitionLoader.ParseDocument(json);
            Assert.IsTrue(BTDefinitionDocumentIds.NormalizeUniqueIds(doc));
            Assert.AreEqual("same", doc.Root.Children[0].Id);
            Assert.AreEqual("same__2", doc.Root.Children[1].Id);
            Assert.IsFalse(BTDefinitionDocumentIds.NormalizeUniqueIds(doc));
        }

        [Test]
        public void CustomNodeType_InvokesRegisteredFactory()
        {
            const string json = @"{
  ""formatVersion"": 1,
  ""root"": {
    ""type"": ""alwaysSuccess"",
    ""data"": {}
  }
}";
            var load = new BTDefinitionLoadContext(true);
            load.RegisterNodeType("alwaysSuccess", (_, __, children) =>
            {
                Assert.AreEqual(0, children.Count);
                return new BTDelegateAction(_ => BTStatus.Success, "custom");
            });

            var bt = BTDefinitionLoader.LoadTree(json, load);
            Assert.AreEqual(BTStatus.Success, bt.Tick(new BTContext()));
        }

        [Test]
        public void RoundTrip_DocumentToJsonFile_AndLoadBack()
        {
            var doc = new BTDefinitionDocument
            {
                FormatVersion = 1,
                Root = new BTNodeDefinition
                {
                    Type = "action",
                    Name = "Leaf",
                    Data = JObject.Parse(@"{ ""handler"": ""x"" }")
                }
            };

            var path = Path.Combine(Path.GetTempPath(), "shibafu_bt_test.json");
            try
            {
                BTDefinitionIO.SaveJson(path, doc);
                var ctx = BTDefinitionLoadContext.CreateWithBuiltIns();
                ctx.RegisterActionHandler("x", _ => BTStatus.Running);
                var tree = BTDefinitionIO.LoadTreeFromJsonFile(path, ctx);
                Assert.AreEqual(BTStatus.Running, tree.Tick(new BTContext()));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
