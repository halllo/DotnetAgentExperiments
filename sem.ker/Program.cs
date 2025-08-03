using Amazon.BedrockRuntime;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text.Json;

var host = CreateHostBuilder().Build();

// Using IChatClient I get function calling via options.Tools.
using (var serviceScope = host.Services.CreateScope())
{
var kernel = serviceScope.ServiceProvider.GetRequiredService<Kernel>();
var chatClient = kernel.GetRequiredService<IChatClient>();

List<ChatMessage> chatHistory = [];
chatHistory.Add(new ChatMessage(ChatRole.System, "You are a helpful AI assistant"));
chatHistory.Add(new ChatMessage(ChatRole.User, "Do I need an umbrella?"));

var invocation = chatClient.GetStreamingResponseAsync(
    messages: chatHistory,
    options: new()
    {
        Temperature = 0f,
        Tools = [AIFunctionFactory.Create([Description("Get the current weather.")]() => kernel.GetRequiredService<WeatherInformation>().GetWeather())]
    });
await foreach (var update in invocation)
{
    Console.Write(update);
}

    //how to get the assistant chat message for following up?
}

// Using IChatCompletionService I dont get function calling via plugins.
using (var serviceScope = host.Services.CreateScope())
{
    var kernel = serviceScope.ServiceProvider.GetRequiredService<Kernel>();
    var chatClient = serviceScope.ServiceProvider.GetRequiredService<IChatCompletionService>();
//#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
//    var chatClient = kernel.GetRequiredService<IChatClient>().AsChatCompletionService(kernel.Services);
//#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    ChatHistory chatHistory = [];
    chatHistory.AddMessage(AuthorRole.System, "You are a helpful AI assistant");
    chatHistory.AddMessage(AuthorRole.User, "Do I need an umbrella?");

    {
        var invocation = chatClient.GetStreamingChatMessageContentsAsync(
            chatHistory: chatHistory,
            executionSettings: new OpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                MaxTokens = 4096,
                Temperature = 0f,
            },
            kernel: kernel);
        await ShowResponseStream(invocation, chatHistory);
    }

    chatHistory.AddMessage(AuthorRole.User, "And sunglasses?");

    {
        var invocation = await chatClient.GetChatMessageContentsAsync(
            chatHistory: chatHistory,
            executionSettings: new OpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                MaxTokens = 4096,
                Temperature = 0f,
            },
            kernel: kernel);
        await ShowResponse(invocation, chatHistory);
    }
}

static async Task ShowResponseStream(IAsyncEnumerable<StreamingChatMessageContent> invocation, ChatHistory chatHistory)
{
    string fullMessage = string.Empty;
    await foreach (var update in invocation)
    {
        if (update.Content != null)
        {
            fullMessage += update.Content;
            Console.Write(update.Content);
        }

        //if (update is Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIStreamingChatMessageContent c)
        //{
        //	if (c.FinishReason != null) Console.WriteLine(Environment.NewLine + c.FinishReason);
        //}
    }

    chatHistory.AddMessage(AuthorRole.Assistant, fullMessage);
}

static async Task ShowResponse(IReadOnlyList<ChatMessageContent> invocation, ChatHistory chatHistory)
{
    string fullMessage = string.Empty;
    foreach (var update in invocation)
    {
        if (update.Content != null)
        {
            fullMessage += update.Content;
            Console.Write(update.Content);
        }

        //if (update is Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatMessageContent c)
        //{
        //	if (c. FinishReason != null) Console.WriteLine(Environment.NewLine + c.FinishReason);
        //}
    }

    chatHistory.AddMessage(AuthorRole.Assistant, fullMessage);
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

        services.AddSingleton<IChatCompletionService>(sp =>
        {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var runtime = sp.GetRequiredService<IAmazonBedrockRuntime>();
            var client = runtime
                .AsIChatClient("anthropic.claude-3-5-sonnet-20240620-v1:0")
                .AsBuilder()
                .UseKernelFunctionInvocation()
                .Build()
                .AsChatCompletionService();
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            return client;
        });

        services.AddBedrockChatClient("anthropic.claude-3-5-sonnet-20240620-v1:0");

        //services.AddBedrockChatCompletionService("anthropic.claude-3-5-sonnet-20240620-v1:0");

        //services.AddOpenAIChatCompletion("gpt-4o-mini", config["OPENAI_API_KEY"]!);

        //services.AddAzureOpenAIChatCompletion(config["AzureOpenAiDeploymentName"]!, config["AzureOpenAiEndpoint"]!, config["AzureOpenAiKey"]!);

        services.AddTransient<WeatherInformation>();

        services.AddKernel()
            .Plugins.AddFromType<WeatherInformation>()
            ;
    });

public class WeatherInformation
{
    private readonly ILogger<WeatherInformation> logger;

    public WeatherInformation(ILogger<WeatherInformation> logger)
    {
        this.logger = logger;
    }

    [KernelFunction]
    [Description("Gets the weather")]
    public string GetWeather()
    {
        string weather = Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining";
        logger.LogInformation(weather);
        return weather;
    }
}

static class JsonExtensions
{
    public static string AsJson(this object o) => JsonSerializer.Serialize(o, o.GetType());
}