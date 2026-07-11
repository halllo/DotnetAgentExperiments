#pragma warning disable MEAI001 // Some Microsoft.Extensions.AI approval/reasoning types are for evaluation only.

using Amazon.BedrockRuntime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using Spectre.Console;
using System.Text.Json;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var provider =
	//"OpenAI"
	"AmazonBedrock"
	;

var host = CreateHostBuilder().Build();
using var serviceScope = host.Services.CreateScope();
var serviceProvider = serviceScope.ServiceProvider;

var chatClient = serviceProvider.GetRequiredKeyedService<IChatClient>(provider);

var historyFile = "history.json";
var forgetRequested = false;

var tools = new AITool[]
{
	AIFunctionFactory.Create(
		name: "forget_history",
		description: "Forget/delete the entire conversation history.",
		method: () =>
		{
			forgetRequested = true;
			if (File.Exists(historyFile))
			{
				File.Delete(historyFile);
				AnsiConsole.Markup($"{Emoji.Known.RecyclingSymbol}  ");
				return "Deleted the conversation history.";
			}

			return "No conversation history to delete.";
		}).RequireApproval()
};

AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
	Name = "Assistant",
	ChatOptions = new ChatOptions
	{
		Instructions = "You are a helpful assistant. Answer short and concise. The shorter the better.",
		Tools = tools,
		Reasoning = provider == "AmazonBedrock"
			? new ReasoningOptions { Effort = ReasoningEffort.Medium }
			: null,// gpt-4o-mini is not a reasoning model, so only ask Bedrock/Claude to think.
	}
});

AnsiConsole.MarkupLine($"[grey27]provider:[/] [grey35]{provider}[/]");

var session = await LoadSessionAsync(agent, historyFile);
if (session.TryGetInMemoryChatHistory(out var previousMessages) && previousMessages is { Count: > 0 })
{
	AnsiConsole.Write(new Rule { Title = "History", Justification = Justify.Left, Style = Style.Parse("grey27") });
	foreach (var previousMessage in previousMessages)
	{
		var text = string.Join(" ", previousMessage.Contents.OfType<TextContent>().Select(c => c.Text));
		if (string.IsNullOrWhiteSpace(text)) continue;
		AnsiConsole.MarkupLine($"[grey27]{previousMessage.Role}:[/] [grey35]{Markup.Escape(text)}[/]");
	}
	AnsiConsole.Write(new Rule { Style = Style.Parse("grey27") });
}

while (true)
{
	AnsiConsole.Markup("[gray]user:[/] ");
	var userInput = Console.ReadLine() ?? "";
	if (string.IsNullOrWhiteSpace(userInput)) break;

	ChatMessage nextMessage = new(ChatRole.User, userInput);
	while (true)
	{
		var updates = new List<AgentResponseUpdate>();
		var isThinking = false;
		var isAnswering = false;

		await foreach (var update in agent.RunStreamingAsync(nextMessage, session))
		{
			updates.Add(update);
			foreach (var content in update.Contents)
			{
				if (content is TextReasoningContent reasoning)
				{
					if (!isThinking)
					{
						AnsiConsole.Markup("[grey]thinking:[/] ");
						isThinking = true;
					}
					AnsiConsole.Markup($"[grey]{Markup.Escape(reasoning.Text)}[/]");
				}
				else if (content is TextContent text)
				{
					if (!isAnswering)
					{
						if (isThinking) Console.WriteLine();
						AnsiConsole.Markup("[gray]assistant:[/] ");
						isAnswering = true;
					}
					Console.Write(text.Text);
				}
			}
		}
		if (isThinking || isAnswering) Console.WriteLine();

		var response = updates.ToAgentResponse();

		var functionApprovals = response.Messages
			.SelectMany(x => x.Contents)
			.OfType<ToolApprovalRequestContent>()
			.Select(approvalRequest =>
			{
				var toolName = (approvalRequest.ToolCall as FunctionCallContent)?.Name ?? approvalRequest.ToolCall.CallId;
				return approvalRequest.CreateResponse(PromptApproval(toolName));
			})
			.ToList();

		if (functionApprovals.Count > 0)
		{
			nextMessage = new ChatMessage(ChatRole.User, [.. functionApprovals]);
		}
		else
		{
			break;
		}
	}

	if (forgetRequested)
	{
		session = await agent.CreateSessionAsync();
		forgetRequested = false;
	}
	else
	{
		await SaveSessionAsync(agent, session, historyFile);
	}
}

Console.WriteLine();
Console.WriteLine("Done");



static IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
	.ConfigureAppConfiguration(cfg =>
	{
		cfg.AddUserSecrets<Program>(optional: true);
		cfg.AddJsonFile("appsettings.local.json", optional: true);
	})
	.ConfigureServices((ctx, services) =>
	{
		var config = ctx.Configuration;

		services.AddKeyedSingleton<IChatClient>("OpenAI", (sp, key) =>
		{
			return new OpenAIClient(config["OPENAI_API_KEY"])
				.GetChatClient("gpt-4o-mini")
				.AsIChatClient();
		});
		services.AddKeyedSingleton<IChatClient>("AmazonBedrock", (sp, key) =>
		{
			var runtime = new AmazonBedrockRuntimeClient(
				awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
				awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
				region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

			return runtime.AsIChatClient("eu.anthropic.claude-sonnet-4-6");
		});
	});

static bool PromptApproval(string toolName)
{
	if (Console.IsInputRedirected)
	{
		AnsiConsole.Markup($"[bold]Approve execution of '{Markup.Escape(toolName)}'? (y/n):[/] ");
		var answer = Console.ReadLine();
		return answer?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
	}

	return AnsiConsole.Prompt(new SelectionPrompt<string>()
		.Title($"[bold]We require approval to execute '{Markup.Escape(toolName)}'.[/]")
		.AddChoices(["Approve", "Reject"])) == "Approve";
}

static async Task<AgentSession> LoadSessionAsync(AIAgent agent, string historyFile)
{
	if (!File.Exists(historyFile))
	{
		return await agent.CreateSessionAsync();
	}

	try
	{
		var json = await File.ReadAllTextAsync(historyFile);
		var element = JsonSerializer.Deserialize<JsonElement>(json);
		return await agent.DeserializeSessionAsync(element);
	}
	catch (Exception ex)
	{
		AnsiConsole.MarkupLine($"[yellow]Could not restore history ({Markup.Escape(ex.Message)}); starting fresh.[/]");
		return await agent.CreateSessionAsync();
	}
}

static async Task SaveSessionAsync(AIAgent agent, AgentSession session, string historyFile)
{
	var element = await agent.SerializeSessionAsync(session);
	var json = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
	await File.WriteAllTextAsync(historyFile, json);
}

static class ApprovalExtensions
{
	public static AIFunction RequireApproval(this AIFunction function) => new ApprovalRequiredAIFunction(function);
}
