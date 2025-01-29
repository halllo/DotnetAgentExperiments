using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

	var bedrock = serviceScope.ServiceProvider.GetRequiredService<IAmazonBedrockRuntime>();
	var response = await bedrock.ConverseAsync(new ConverseRequest
	{
		ModelId = "anthropic.claude-3-sonnet-20240229-v1:0",
		Messages = messages,
		ToolConfig = new ToolConfiguration
		{
			//taken from https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use-inference-call.html
			Tools = new List<Tool>
			{
				new Tool
				{
					ToolSpec = new ToolSpecification
					{
						Name = "GetWeather",
						Description = "Gets the weather",
						InputSchema = new ToolInputSchema
						{
							Json = Amazon.Runtime.Documents.Document.FromObject(new
							{
								Type = "object",
								Properties = new Dictionary<string, object>
								{
									{ "sign", new {
										Type = "string",
										Description = "The call sign for the radio station for which you want the most popular song. Example calls signs are WZPZ and WKRP."
									} }
								},
								Required = new string[]
								{
									 "sign"
								},
							}),
						},
					}
				}
			}
		}
	});

	Console.WriteLine(response.StopReason);
	var responseText = string.Concat(response.Output.Message.Content.Select(c => c.Text));
	Console.WriteLine(responseText);
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