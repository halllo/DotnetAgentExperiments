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
var host = CreateHostBuilder().Build();
using (var serviceScope = host.Services.CreateScope())
{
	var serviceProvider = serviceScope.ServiceProvider;
	var client = serviceProvider.GetRequiredKeyedService<IChatClient>(
		//"OpenAI"
		"AmazonBedrock"
	);

	var historyFile = "history.json";
	ChatClientAgentThread thread = null!;
	ChatClientAgent agent = null!;

	agent = client.CreateAIAgent(
		instructions: "You are a helpful assistant. Answer short and concise. The shorter the better.",
		tools:
		[
			AIFunctionFactory.Create(
				name: "forget_history",
				method: () =>
				{
					if (File.Exists(historyFile))
					{
						File.Delete(historyFile);
						thread = (ChatClientAgentThread)agent.GetNewThread();
						AnsiConsole.Markup($"{Emoji.Known.RecyclingSymbol}  ");
						return "Deleted the conversation history.";
					}
					else
					{
						return "No conversation history to delete.";
					}
				}).RequireApproval()
		]);

	thread = (ChatClientAgentThread)(File.Exists(historyFile)
		? agent.DeserializeThread(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(historyFile)))
		: agent.GetNewThread());

	var historyMessages = await (thread.MessageStore?.GetMessagesAsync() ?? Task.FromResult(Enumerable.Empty<ChatMessage>()));
	if (historyMessages.Any())
	{
		AnsiConsole.Write(new Rule { Title = "History", Justification = Justify.Left, Style = Style.Parse("grey27") });
		foreach (var previousMessage in historyMessages)
		{
			AnsiConsole.MarkupLine($"[grey27]{previousMessage.Role}:[/] [grey35]{Markup.Escape(string.Join(" ", previousMessage.Contents.Select(c => c.ToString())))}[/]");
		}
		AnsiConsole.Write(new Rule { Style = Style.Parse("grey27") });
	}

	string userInput;
	while (true)
	{
		AnsiConsole.Markup("[gray]user:[/] ");
		userInput = Console.ReadLine() ?? "";
		if (string.IsNullOrWhiteSpace(userInput)) break;
		var userMessage = new ChatMessage(ChatRole.User, userInput);

		while (true)
		{
			var updates = new List<AgentRunResponseUpdate>();
			var stream = agent.RunStreamingAsync(userMessage, thread);
			AnsiConsole.Markup("[gray]assistant:[/] ");
			await foreach (var update in stream)
			{
				updates.Add(update);
				Console.Write(update);
			}
			Console.WriteLine();
			var response = updates.ToAgentRunResponse();

			var functionApprovals = response.Messages
				.SelectMany(x => x.Contents)
				.OfTypeApprovalRequest()
				.Select(approvalRequest =>
				{
					var toolName = (approvalRequest.ToolCall as FunctionCallContent)?.Name ?? approvalRequest.ToolCall.CallId;
					var approved = AnsiConsole.Prompt(new SelectionPrompt<string>()
						.Title($"[bold]We require approval to execute '{toolName}'.[/]")
						.AddChoices(["Approve", "Reject"])) == "Approve";
					return approvalRequest.CreateResponse(approved);
				})
				.ToList();

			if (functionApprovals.Any())
			{
				var approvalMessage = new ChatMessage(ChatRole.User, [.. functionApprovals]);
				userMessage = approvalMessage;
			}
			else break;
		}
	}

	File.WriteAllText(historyFile, thread.Serialize().ToString());
}

Console.WriteLine();
Console.WriteLine("Done");



static IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
	.ConfigureAppConfiguration(cfg =>
	{
		cfg.AddJsonFile("appsettings.local.json", optional: true);
	})
	.ConfigureServices((ctx, services) =>
	{
		var config = ctx.Configuration;

		services.AddKeyedSingleton("OpenAI", (sp, key) =>
		{
			var openAi = new OpenAIClient(config["OPENAI_API_KEY"]).GetChatClient("gpt-4o-mini");
			var client = openAi.AsIChatClient();
			return client;
		});
		services.AddKeyedSingleton("AmazonBedrock", (sp, key) =>
		{
			var runtime = new AmazonBedrockRuntimeClient(
				awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
				awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
				region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

			var client = runtime.AsIChatClient("eu.anthropic.claude-sonnet-4-20250514-v1:0");
			return client;
		});
	});

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
static class ApprovalExtensions
{
	public static AIFunction RequireApproval(this AIFunction function) => new ApprovalRequiredAIFunction(function);
	public static IEnumerable<ToolApprovalRequestContent> OfTypeApprovalRequest(this IEnumerable<AIContent> contents) => contents.OfType<ToolApprovalRequestContent>();
}
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
