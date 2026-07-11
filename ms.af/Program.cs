using Amazon.BedrockRuntime;
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
	var historyMessages = LoadHistory(historyFile);
	var chatHistory = new List<ChatMessage>(historyMessages);
	var tools = new AITool[]
	{
		AIFunctionFactory.Create(
			name: "forget_history",
			method: () =>
			{
				chatHistory.Clear();
				if (File.Exists(historyFile))
				{
					File.Delete(historyFile);
					AnsiConsole.Markup($"{Emoji.Known.RecyclingSymbol}  ");
					return "Deleted the conversation history.";
				}

				return "No conversation history to delete.";
			}).RequireApproval()
	};

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
		chatHistory.Add(new ChatMessage(ChatRole.User, userInput));

		while (true)
		{
			var updates = new List<ChatResponseUpdate>();
			var stream = client.GetStreamingResponseAsync(
				messages: chatHistory,
				options: new ChatOptions
				{
					Instructions = "You are a helpful assistant. Answer short and concise. The shorter the better.",
					Tools = tools,
					Temperature = 0f,
				});
			AnsiConsole.Markup("[gray]assistant:[/] ");
			await foreach (var update in stream)
			{
				updates.Add(update);
				Console.Write(update);
			}
			Console.WriteLine();
			var response = updates.ToChatResponse();
			chatHistory.AddMessages(response);

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
				chatHistory.Add(new ChatMessage(ChatRole.User, [.. functionApprovals]));
			}
			else break;
		}
	}

	SaveHistory(historyFile, chatHistory);
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

		services.AddKeyedSingleton("OpenAI", (sp, key) =>
		{
			var openAi = new OpenAIClient(config["OPENAI_API_KEY"]).GetChatClient("gpt-4o-mini");
			var client = openAi
				.AsIChatClient()
				.AsBuilder()
				.UseFunctionInvocation()
				.Build();
			return client;
		});
		services.AddKeyedSingleton("AmazonBedrock", (sp, key) =>
		{
			var runtime = new AmazonBedrockRuntimeClient(
				awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
				awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
				region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

			var client = runtime
				.AsIChatClient("eu.anthropic.claude-sonnet-4-20250514-v1:0")
				.AsBuilder()
				.UseFunctionInvocation()
				.Build();
			return client;
		});
	});

static IReadOnlyList<ChatMessage> LoadHistory(string historyFile)
{
	if (!File.Exists(historyFile))
	{
		return [];
	}

	var json = File.ReadAllText(historyFile);
	return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? [];
}

static void SaveHistory(string historyFile, IReadOnlyList<ChatMessage> chatHistory)
{
	var json = JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions { WriteIndented = true });
	File.WriteAllText(historyFile, json);
}

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
static class ApprovalExtensions
{
	public static AIFunction RequireApproval(this AIFunction function) => new ApprovalRequiredAIFunction(function);
	public static IEnumerable<ToolApprovalRequestContent> OfTypeApprovalRequest(this IEnumerable<AIContent> contents) => contents.OfType<ToolApprovalRequestContent>();
}
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
