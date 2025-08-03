using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using System.ComponentModel;

var host = CreateHostBuilder().Build();
using (var serviceScope = host.Services.CreateScope())
{
    var chatClient = serviceScope.ServiceProvider.GetRequiredService<IChatClient>();

    var chatMessages = new List<ChatMessage>
    {
        new(ChatRole.System, "You are a helpful AI assistant"),
        new(ChatRole.User, "Do I need an umbrella?"),
    };

    var invocation = chatClient.GetStreamingResponseAsync(
        messages: chatMessages,
        options: new()
        {
            Tools = [AIFunctionFactory.Create(GetWeather)]
        });

    await foreach (var update in invocation)
    {
        Console.Write(update);
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

        services.AddSingleton(sp =>
        {
            var openAiClient = new OpenAIClient(config["OPENAI_API_KEY"]).GetChatClient("gpt-4o-mini").AsIChatClient();
            var client = new ChatClientBuilder(openAiClient)
                .UseFunctionInvocation()
                .Build();

            return client;
        });

    });

[Description("Gets the weather")]
static string GetWeather() => Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining";