using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using aws.bed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

var host = CreateHostBuilder().Build();
using (var serviceScope = host.Services.CreateScope())
{
	var messages = new List<Message>
	{
		new Message
		{
			Role = ConversationRole.User,
			Content = new List<ContentBlock> { new ContentBlock { Text = "Get the most popular song played on a radio station." } }
		}
	};

	var getSongTool = [Description("Gets the current song on the radio")]
	(
		[Description("The call sign for the radio station for which you want the most popular song. Example calls signs are WZPZ and WKRP."), Required] string sign
	) => "Random Song 1";

	var bedrock = serviceScope.ServiceProvider.GetRequiredService<IAmazonBedrockRuntime>();
	var response = await bedrock.ConverseAsync(new ConverseRequest
	{
		ModelId = "anthropic.claude-3-sonnet-20240229-v1:0",
		Messages = messages,
		ToolConfig = new ToolConfiguration
		{
			Tools = new List<Tool>
			{
				UsableTool.From(getSongTool),
			}
		}
	});

	Console.WriteLine(response.StopReason);

	var texts = response.Output.Message.Content.Select(c => c.Text);
	Console.WriteLine(string.Concat(texts));

	var toolUses = response.Output.Message.Content.Select(c => c.ToolUse).Where(t => t != null);
	var toolUse = toolUses.Single();
	var inputs = toolUse.Input.AsDictionary();
	Console.WriteLine($"{toolUse.ToolUseId}: {toolUse.Name}: {string.Join(",", inputs.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

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
