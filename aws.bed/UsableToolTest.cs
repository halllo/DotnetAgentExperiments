using Amazon.BedrockRuntime.Model;
using System.ComponentModel.DataAnnotations;
using static aws.bed.UsableTool;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace Tests
{
	[TestClass]
	public sealed class UsableToolTest
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

			AssertEqual(expectedTool, From(getSongTool));
		}

		[TestMethod]
		public void StringAndIntToString()
		{
			var rateSongTool = [Description("Rate a song")]
			(
				[Description("The song name"), Required] string song,
				[Required] int rating
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
									type = "int",
									description = ""
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

			AssertEqual(expectedTool, From(rateSongTool));
		}
	}
}
