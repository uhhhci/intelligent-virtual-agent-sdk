using NUnit.Framework;
using IVH.Core.IntelligentVirtualAgent.Tools;
using Newtonsoft.Json.Linq;

namespace IVH.Core.Tests.Tools
{
    /// <summary>
    /// Tests the shape of <see cref="GeminiDynamicTool"/> defaults and parameter-schema parsing.
    /// Protects against accidental changes to the tool serialization contract.
    /// </summary>
    public class GeminiDynamicToolTests
    {
        [Test]
        public void NewTool_HasValidDefaultParameterSchema()
        {
            var tool = new GeminiDynamicTool();

            Assert.IsNotNull(tool.parametersJson);
            JObject schema = null;
            Assert.DoesNotThrow(() => schema = JObject.Parse(tool.parametersJson));
            Assert.AreEqual("object", schema["type"]?.ToString());
            Assert.IsNotNull(schema["properties"]);
        }

        [Test]
        public void ToolName_DefaultsToNull()
        {
            var tool = new GeminiDynamicTool();
            Assert.IsNull(tool.toolName);
        }

        [Test]
        public void Description_DefaultsToNull()
        {
            var tool = new GeminiDynamicTool();
            Assert.IsNull(tool.description);
        }
    }
}
