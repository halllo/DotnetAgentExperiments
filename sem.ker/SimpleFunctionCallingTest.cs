using Amazon.BedrockRuntime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace sem.ker
{
	[TestClass]
	public class SimpleFunctionCallingTest
	{
		[TestMethod]
		[DataRow("Microsoft.SemanticKernel.Connectors.Amazon_ChatClient")]
		[DataRow("AWSSDK.Extensions.Bedrock.MEAI_UseKernelFunctionInvocation")]
		[DataRow("Microsoft.SemanticKernel.Connectors.OpenAI_ChatClient")]
		public async Task ChatClientInvokesTool(string aiProvider)
		{
			var host = CreateHostBuilder().Build();
			using (var serviceScope = host.Services.CreateScope())
			{
				var toolCallMemory = serviceScope.ServiceProvider.GetRequiredService<ToolCallMemory>();
				Assert.IsFalse(toolCallMemory.ToolCalled);

				var kernel = serviceScope.ServiceProvider.GetRequiredService<Kernel>();
				var chatClient = kernel.GetRequiredService<IChatClient>(aiProvider);

				List<ChatMessage> chatHistory = [];
				chatHistory.Add(new ChatMessage(ChatRole.System, "You are a helpful AI assistant"));
				chatHistory.Add(new ChatMessage(ChatRole.User, "Do I need an umbrella?"));

				var invocation = chatClient.GetStreamingResponseAsync(
					messages: chatHistory,
					options: new()
					{
						Temperature = 0f,
						Tools = [
							AIFunctionFactory.Create(
								method: [Description("Get the current weather.")]() =>
								{
									return kernel.GetRequiredService<WeatherInformation>().GetWeather();
								})
						]
					});

				await foreach (var update in invocation)
				{
					Console.Write(update);
				}

				Assert.IsTrue(toolCallMemory.ToolCalled);
			}
		}

		[TestMethod]
		[DataRow("Microsoft.SemanticKernel.Connectors.Amazon_ChatCompletion", true)]
		[DataRow("AWSSDK.Extensions.Bedrock.MEAI_UseKernelFunctionInvocation_AsChatCompletionService", false)]
		[DataRow("AWSSDK.Extensions.Bedrock.MEAI_UseFunctionInvocation_AsChatCompletionService", false)]
		[DataRow("CustomBedrockClient_AsChatCompletionService", false)]
		[DataRow("Microsoft.SemanticKernel.Connectors.OpenAI_ChatCompletion", false)]
		public async Task ChatCompletionInvokesTool(string aiProvider, bool add_max_tokens_to_sample)
		{
			var host = CreateHostBuilder().Build();
			using (var serviceScope = host.Services.CreateScope())
			{
				var toolCallMemory = serviceScope.ServiceProvider.GetRequiredService<ToolCallMemory>();
				Assert.IsFalse(toolCallMemory.ToolCalled);

				var kernel = serviceScope.ServiceProvider.GetRequiredService<Kernel>();
				var chatCompletion = kernel.GetRequiredService<IChatCompletionService>(aiProvider);

				ChatHistory chatHistory = [];
				chatHistory.AddMessage(AuthorRole.System, "You are a helpful AI assistant");
				chatHistory.AddMessage(AuthorRole.User, "Do I need an umbrella?");

				var invocation = chatCompletion.GetStreamingChatMessageContentsAsync(
					chatHistory: chatHistory,
					executionSettings: new OpenAIPromptExecutionSettings()
					{
						FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
						MaxTokens = 4096,
						Temperature = 0f,
						ExtensionData = add_max_tokens_to_sample ? new Dictionary<string, object>() {
							{ "max_tokens_to_sample", 4096 }
						} : null,
					},
					kernel: kernel);

				await foreach (var update in invocation)
				{
					if (update.Content != null)
					{
						Console.Write(update.Content);
					}
				}

				Assert.IsTrue(toolCallMemory.ToolCalled);
			}
		}

		public class WeatherInformation
		{
			private readonly ILogger<WeatherInformation> logger;
			private readonly ToolCallMemory toolCallMemory;

			public WeatherInformation(ILogger<WeatherInformation> logger, ToolCallMemory toolCallMemory)
			{
				this.logger = logger;
				this.toolCallMemory = toolCallMemory;
			}

			[KernelFunction]
			[Description("Gets the weather")]
			public string GetWeather()
			{
				toolCallMemory.ToolCalled = true;
				string weather = "It's cloudy now and raining later";
				logger.LogInformation(weather);
				return weather;
			}
		}

		public class ToolCallMemory
		{
			public bool ToolCalled { get; set; } = false;
		}

		static IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
			.ConfigureAppConfiguration(cfg =>
			{
				cfg.AddJsonFile("appsettings.local.json", optional: true);
			})
			.ConfigureServices((ctx, services) =>
			{
				var config = ctx.Configuration;

				services.AddBedrockChatCompletionService(serviceId: "Microsoft.SemanticKernel.Connectors.Amazon_ChatCompletion",
					modelId: "anthropic.claude-3-5-sonnet-20240620-v1:0",
					bedrockRuntime: new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!)));

				services.AddBedrockChatClient(serviceId: "Microsoft.SemanticKernel.Connectors.Amazon_ChatClient",
					modelId: "anthropic.claude-3-5-sonnet-20240620-v1:0",
					bedrockRuntime: new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!)));

				services.AddKeyedSingleton<IChatCompletionService>("AWSSDK.Extensions.Bedrock.MEAI_UseKernelFunctionInvocation_AsChatCompletionService", (sp, key) =>
				{
					var runtime = new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

					var client = runtime
						.AsIChatClient("anthropic.claude-3-5-sonnet-20240620-v1:0")
						.AsBuilder()
						.UseKernelFunctionInvocation()
						.Build()
						.AsChatCompletionService();

					return client;
				});

				services.AddKeyedSingleton<IChatCompletionService>("AWSSDK.Extensions.Bedrock.MEAI_UseFunctionInvocation_AsChatCompletionService", (sp, key) =>
				{
					var runtime = new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

					var client = runtime
						.AsIChatClient("anthropic.claude-3-5-sonnet-20240620-v1:0")
						.AsBuilder()
						.UseFunctionInvocation()
						.Build()
						.AsChatCompletionService();

					return client;
				});

				services.AddKeyedSingleton<IChatClient>("AWSSDK.Extensions.Bedrock.MEAI_UseKernelFunctionInvocation", (sp, key) =>
				{
					var runtime = new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

					var client = runtime
						.AsIChatClient("anthropic.claude-3-5-sonnet-20240620-v1:0")
						.AsBuilder()
						.UseKernelFunctionInvocation()
						.Build();

					return client;
				});

				services.AddKeyedSingleton<IChatCompletionService>("CustomBedrockClient_AsChatCompletionService", (sp, key) =>
				{
					var runtime = new AmazonBedrockRuntimeClient(
						awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
						awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
						region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

					var client = new CustomBedrockChatClient(runtime, "anthropic.claude-3-5-sonnet-20240620-v1:0")
						.AsBuilder()
						.UseFunctionInvocation()
						.Build()
						.AsChatCompletionService();

					return client;
				});

				services.AddOpenAIChatCompletion(serviceId: "Microsoft.SemanticKernel.Connectors.OpenAI_ChatCompletion",
					modelId: "gpt-4o-mini",
					apiKey: config["OPENAI_API_KEY"]!);

				services.AddOpenAIChatClient(serviceId: "Microsoft.SemanticKernel.Connectors.OpenAI_ChatClient",
					modelId: "gpt-4o-mini",
					apiKey: config["OPENAI_API_KEY"]!);

				services.AddSingleton<ToolCallMemory>();

				services.AddTransient<WeatherInformation>();

				services.AddKernel()
					.Plugins.AddFromType<WeatherInformation>()
					;
			});
	}
}
