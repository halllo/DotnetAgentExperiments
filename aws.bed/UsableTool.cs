using Amazon.BedrockRuntime.Model;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using ThirdParty.Json.LitJson;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace aws.bed
{
	public static class UsableTool
	{
		public static Tool From(Delegate tool, [CallerArgumentExpression("tool")] string toolArgumentExpression = "")
		{
			var method = tool.GetMethodInfo();
			var methodDescription = method.GetCustomAttributes<DescriptionAttribute>().Single().Description;
			var methodParameters = method.GetParameters();
			var toolPropertiesDictionary = methodParameters.ToDictionary(p => p.Name ?? string.Empty, p => new
			{
				Type = p.ParameterType,
				Description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty,
				Required = p.GetCustomAttribute<RequiredAttribute>() != null,
			});

			return new Tool
			{
				ToolSpec = new ToolSpecification
				{
					Name = toolArgumentExpression,
					Description = methodDescription,
					InputSchema = new ToolInputSchema
					{
						//taken from https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use-inference-call.html
						Json = Amazon.Runtime.Documents.Document.FromObject(new
						{
							type = "object",
							properties = toolPropertiesDictionary.ToDictionary(kvp => kvp.Key, kvp => (object)new
							{
								type = kvp.Value.Type.Name.ToLowerInvariant(),
								description = kvp.Value.Description,
							}),
							required = toolPropertiesDictionary.Where(kvp => kvp.Value.Required).Select(kvp => kvp.Key).ToArray(),
						}),
					},
				}
			};
		}

		public static void AssertEqual(Tool expected, Tool actual)
		{
			Assert.AreEqual(expected.ToolSpec.Name, actual.ToolSpec.Name, "'name' mismatch");
			Assert.AreEqual(expected.ToolSpec.Description, actual.ToolSpec.Description, "'description' mismatch");

			var expectedInputSchema = expected.ToolSpec.InputSchema.Json.AsDictionary();
			var actualInputSchema = actual.ToolSpec.InputSchema.Json.AsDictionary();
			Assert.AreEqual(JsonMapper.ToJson(expectedInputSchema), JsonMapper.ToJson(actualInputSchema), "'inputSchema' mismatch");
			Assert.AreEqual(expectedInputSchema["type"], actualInputSchema["type"], "'type' mismatch");

			var expectedInputProperties = expectedInputSchema["properties"].AsDictionary();
			var actualInputProperties = actualInputSchema["properties"].AsDictionary();
			Assert.AreEqual(JsonMapper.ToJson(expectedInputProperties), JsonMapper.ToJson(actualInputProperties), "'properties' mismatch");
			foreach (var property in expectedInputProperties)
			{
				var expectedValue = property.Value.AsDictionary();
				var actualValue = actualInputProperties[property.Key].AsDictionary();
				Assert.AreEqual(JsonMapper.ToJson(expectedValue), JsonMapper.ToJson(actualValue), $"property '{property.Key}' mismatch");
			}

			var expectedInputRequired = expectedInputSchema["required"].AsList().Select(d => d.AsString()).ToList();
			var actualInputRequired = actualInputSchema["required"].AsList().Select(d => d.AsString()).ToList();
			CollectionAssert.AreEqual(expectedInputRequired, actualInputRequired, "'required' mismatch");
		}
	}
}