using Amazon.BedrockRuntime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

namespace ms.ext.ai
{
	[TestClass]
	public class SimpleFunctionCallingTest
	{
		[TestMethod]
		[DataRow("Microsoft.Extensions.AI.OpenAI")]
		[DataRow("AWSSDK.Extensions.Bedrock.MEAI")]
		[DataRow("CustomBedrockChatClient")]
		public async Task ToolIsInvoked(string aiProvider)
		{
			var host = CreateHostBuilder().Build();
			using (var serviceScope = host.Services.CreateScope())
			{
				var chatClient = serviceScope.ServiceProvider.GetRequiredKeyedService<IChatClient>(aiProvider);

				var chatMessages = new List<ChatMessage>
				{
					new(ChatRole.System, "You are a helpful AI assistant"),
					new(ChatRole.User, "Do I need an umbrella?"),
				};

				bool toolCalled = false;
				var invocation = chatClient.GetStreamingResponseAsync(
					messages: chatMessages,
					options: new()
					{
						Tools = [AIFunctionFactory.Create(
							method: () => {
								toolCalled = true;
								return "It's cloudy now and raining later";
							},
							name: "GetWeather",
							description: "Gets the weather.")]
					});

				await foreach (var update in invocation)
				{
					Console.Write(update);
				}

				Assert.IsTrue(toolCalled);
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

				services.AddKeyedSingleton<IChatClient>("Microsoft.Extensions.AI.OpenAI", (sp, key) =>
				{
					var openAiClient = new OpenAIClient(config["OPENAI_API_KEY"]).GetChatClient("gpt-4o-mini");

					var client = openAiClient
						.AsIChatClient()
						.AsBuilder()
						.UseFunctionInvocation()
						.Build();

					return client;
				});

				services.AddKeyedSingleton<IChatClient>("AWSSDK.Extensions.Bedrock.MEAI", (sp, key) =>
				{
					var runtime = new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

					var client = runtime
						.AsIChatClient("anthropic.claude-3-5-sonnet-20240620-v1:0")
						.AsBuilder()
						.UseFunctionInvocation()
						.Build();

					return client;
				});

				services.AddKeyedSingleton<IChatClient>("CustomBedrockChatClient", (sp, key) =>
				{
					var runtime = new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

					var client = new CustomBedrockChatClient(runtime, "anthropic.claude-3-5-sonnet-20240620-v1:0")
						.AsBuilder()
						.UseFunctionInvocation()
						.Build();

					return client;
				});
			});
	}
}
