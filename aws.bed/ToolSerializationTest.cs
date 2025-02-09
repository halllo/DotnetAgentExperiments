using Amazon.BedrockRuntime.Model;
using aws.bed;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ThirdParty.Json.LitJson;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace Tests
{
	/// <summary>
	/// Scenarios taken from https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use-inference-call.html
	/// </summary>
	[TestClass]
	public sealed class ToolSerializationTest
	{
		[TestMethod]
		public void StringToString()
		{
			var getSongTool = [Description("Gets the current song on the radio")]
			(
				[Description("The call sign for the radio station for which you want the most popular song. Example calls signs are WZPZ and WKRP."), Required] string sign
			) => "Random Song 1";

			var expectedTool = new Tool
			{
				ToolSpec = new ToolSpecification
				{
					Name = "getSongTool",
					Description = "Gets the current song on the radio",
					InputSchema = new ToolInputSchema
					{
						Json = Amazon.Runtime.Documents.Document.FromObject(new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								{ "sign", new {
									type = "string",
									description = "The call sign for the radio station for which you want the most popular song. Example calls signs are WZPZ and WKRP."
								} }
							},
							required = new string[]
							{
								"sign"
							},
						}),
					},
				}
			};

			AssertEqual(expectedTool, Agent.Tool.From(getSongTool).GetDefinition());
		}

		[TestMethod]
		public void StringAndIntToString()
		{
			var rateSongTool = [Description("Rate a song")]
			(
				[Description("The song name"), Required] string song,
				[Required] string rating
			) => "Rated!";

			var expectedTool = new Tool
			{
				ToolSpec = new ToolSpecification
				{
					Name = "rateSongTool",
					Description = "Rate a song",
					InputSchema = new ToolInputSchema
					{
						Json = Amazon.Runtime.Documents.Document.FromObject(new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								{ "song", new {
									type = "string",
									description = "The song name"
								} },
								{ "rating", new {
									type = "string",
									description = "rating"
								} },
							},
							required = new string[]
							{
								"song",
								"rating"
							},
						}),
					},
				}
			};

			AssertEqual(expectedTool, Agent.Tool.From(rateSongTool).GetDefinition());
		}

		[TestMethod]
		public void InlineMethod()
		{
			var expectedTool = new Tool
			{
				ToolSpec = new ToolSpecification
				{
					Name = "RateASong",
					Description = "Rate a song",
					InputSchema = new ToolInputSchema
					{
						Json = Amazon.Runtime.Documents.Document.FromObject(new
						{
							type = "object",
							properties = new Dictionary<string, object>
							{
								{ "song", new {
									type = "string",
									description = "song"
								} },
								{ "rating", new {
									type = "string",
									description = "rating"
								} },
							},
							required = new string[]
							{
							},
						}),
					},
				}
			};

			var usableTool = Agent.Tool.From([Description("Rate a song")] (string song, string rating) => "Rated!");

			AssertEqual(expectedTool, usableTool.GetDefinition());
		}

		private static void AssertEqual(Tool expected, Tool actual)
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
				var expectedValue = property.Value.AsDictionary().ToDictionary(v => v.Key, v => v.Value.ToString());
				var actualValue = actualInputProperties[property.Key].AsDictionary().ToDictionary(v => v.Key, v => v.Value.ToString());
				Assert.AreEqual(JsonSerializer.Serialize(expectedValue), JsonSerializer.Serialize(actualValue), $"property '{property.Key}' mismatch");
			}

			var expectedInputRequired = expectedInputSchema["required"].AsList().Select(d => d.AsString()).ToList();
			var actualInputRequired = actualInputSchema["required"].AsList().Select(d => d.AsString()).ToList();
			CollectionAssert.AreEqual(expectedInputRequired, actualInputRequired, "'required' mismatch");
		}
	}
}
