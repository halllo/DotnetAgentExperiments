using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace aws.bed
{
	[TestClass]
	public class AwsSdkTest
	{
		[TestMethod]
		public async Task InvokesTools()
		{
			var host = CreateHostBuilder().Build();
			using (var serviceScope = host.Services.CreateScope())
			{
				var tool = new Tool()
				{
					ToolSpec = new ToolSpecification
					{
						Name = "RegisterPerson",
						Description = "Registers a person.",
						InputSchema = new ToolInputSchema
						{
							Json = Document.FromObject(new
							{
								type = "object",
								properties = new Dictionary<string, object>
								{
									{ "name", new {
										type = "string",
										description = "The name of the person."
									} },
									{ "age", new {
										type = "integer",
										description = "The age of the person."
									} },
									{ "address", new {
										type = new string[]
										{
											"object",
											"null"
										},
										description = "The address of the person.",
										properties = new Dictionary<string, object>
										{
											{ "street", new {
												type = "string"
											} },
											{ "city", new {
												type = "string"
											} },
										},
										required = new string[]
										{
											"city"
										},
									} },
								},
								required = new string[]
								{
									"name",
									"age"
								},
							}),
						},
					}
				};

				var messages = new List<Message>
				{
					new()
					{
						Role = ConversationRole.User,
						Content = [new ContentBlock { Text = "Its March 2025. I would like to register Manuel Naujoks (born in September 1986) from Karlsruhe." }]
					},
				};

				var bedrock = serviceScope.ServiceProvider.GetRequiredService<IAmazonBedrockRuntime>();
				var response = await bedrock.ConverseAsync(new ConverseRequest
				{
					ModelId = "anthropic.claude-3-5-sonnet-20240620-v1:0",
					Messages = messages,
					ToolConfig = new ToolConfiguration { Tools = [tool] },
					InferenceConfig = new InferenceConfiguration() { Temperature = 0.0F }
				});

				var responseMessage = response.Output.Message;
				Assert.AreEqual(2, responseMessage.Content.Count);

				var text = responseMessage.Content[0].Text;
				Console.WriteLine(text);

				var toolUse = responseMessage.Content[1].ToolUse;
				var parameters = toolUse.Input.AsDictionary();
				Assert.AreEqual("Manuel Naujoks", parameters["name"].AsString());
				Assert.AreEqual(38, parameters["age"].AsInt());
				var address = parameters["address"].AsDictionary();
				Assert.IsNotNull(address);
				Assert.AreEqual("Karlsruhe", address["city"].AsString());
			}
		}

		static IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
			.ConfigureAppConfiguration(cfg =>
			{
				cfg.AddJsonFile("appsettings.local.json", optional: true);
			})
			.ConfigureServices((ctx, services) =>
			{
				var config = ctx.Configuration;

				services.AddSingleton<IAmazonBedrockRuntime>(sp =>
				{
					return new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));
				});
			});
	}
}
