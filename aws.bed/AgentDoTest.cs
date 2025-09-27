using AgentDo;
using AgentDo.Bedrock;
using Amazon.BedrockRuntime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace aws.bed
{
	[TestClass]
	public class AgentDoTest
	{
		[TestMethod]
		public async Task InvokesTools()
		{
			var host = CreateHostBuilder().Build();
			using (var serviceScope = host.Services.CreateScope())
			{
				var ratedSong = "";

				var agent = serviceScope.ServiceProvider.GetRequiredService<IAgent>();
				var result = await agent.Do(
					task: "Get the most popular song played on a radio station RGBG and rate it as bad.",
					tools:
					[
						Tool.From([Description("Get radio song")]([Description("The call sign for the radio station for which you want the most popular song."), Required] string sign)
						=> new { songName = "Random Song 1" }),

						Tool.From([Description("Rate a song")](string song, string rating)
						=> {
							ratedSong = song;
							return "Rated!";
						}),
					],
					events: ConsoleOut());

				Assert.AreEqual("Random Song 1", ratedSong);
			}
		}

		static Events ConsoleOut(bool streaming = true) => new Events
		{
			BeforeMessage = async (role, message) => AnsiConsole.Markup($"[gray]{role}:[/] "),
			OnMessageDelta = async (role, message) => AnsiConsole.Markup(message),
			AfterMessage = async (role, message) => AnsiConsole.MarkupLine(streaming ? string.Empty : $"[gray]{role}:[/] {message}"),
			BeforeToolCall = async (role, tool, toolUse, context, parameters) =>
			{
				AnsiConsole.MarkupLine($"[gray]{role}:[/] [cyan]🛠️{tool.Name}({Markup.Escape(JsonSerializer.Serialize(parameters))})...[/]");
			},
			AfterToolCall = async (role, tool, toolUse, context, result) =>
			{
				AnsiConsole.MarkupLine($"[gray]{toolUse.ToolUseId}: {Markup.Escape(JsonSerializer.Serialize(result))}[/]");
			},
		};

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

				services.Configure<BedrockAgentOptions>(o =>
				{
					o.ModelId = "anthropic.claude-3-5-sonnet-20240620-v1:0";
					o.Streaming = true;
				});

				services.AddTransient<IAgent, BedrockAgent>();
			});
	}
}
