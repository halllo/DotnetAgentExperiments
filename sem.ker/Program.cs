using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Text.Json;

var host = CreateHostBuilder().Build();
using (var serviceScope = host.Services.CreateScope())
{
    var kernel = serviceScope.ServiceProvider.GetRequiredService<Kernel>();
    var chatClient = serviceScope.ServiceProvider.GetRequiredService<IChatCompletionService>();

    ChatHistory chatHistory = [];
    chatHistory.AddMessage(AuthorRole.System, "You are a helpful AI assistant");
    chatHistory.AddMessage(AuthorRole.User, "Do I need an umbrella?");

    {
        var invocation = chatClient.GetStreamingChatMessageContentsAsync(
            chatHistory: chatHistory,
            executionSettings: new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
            kernel: kernel);

        await StreamResponse(invocation, chatHistory);
    }

    chatHistory.AddMessage(AuthorRole.User, "And sunglasses?");

    {
        var invocation = chatClient.GetStreamingChatMessageContentsAsync(
            chatHistory: chatHistory,
            executionSettings: new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
            kernel: kernel);

        await StreamResponse(invocation, chatHistory);
    }
}

static async Task StreamResponse(IAsyncEnumerable<StreamingChatMessageContent> invocation, ChatHistory chatHistory)
{
    string fullMessage = string.Empty;
    await foreach (var update in invocation)
    {
        if (update.Content != null)
        {
            fullMessage += update.Content;
            Console.Write(update.Content);
        }
        else
        {
            //Console.WriteLine(Environment.NewLine + update.AsJson());
        }

        if (update is Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIStreamingChatMessageContent c)
        {
            if (c.FinishReason != null) Console.WriteLine(Environment.NewLine + c.FinishReason);
        }
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

        //#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        //        services.AddBedrockChatCompletionService("amazon.titan-text-premier-v1:0");
        //#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        services.AddOpenAIChatCompletion("gpt-4o-mini", config["OPENAI_API_KEY"]!);

        services.AddTransient<WeatherInformation>();

        services.AddKernel().Plugins.AddFromType<WeatherInformation>();
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