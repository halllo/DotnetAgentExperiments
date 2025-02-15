using AgentDo;
using Amazon.BedrockRuntime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

var host = CreateHostBuilder().Build();
using (var serviceScope = host.Services.CreateScope())
{
	var agent = serviceScope.ServiceProvider.GetRequiredService<IAgent>();

	await agent.Do(
		task: "Get the most popular song played on a radio station RGBG and rate it as bad.",
		tools:
		[
			Tool.From([Description("Get radio song")]([Description("The call sign for the radio station for which you want the most popular song."), Required] string sign)
			=> new { songName = "Random Song 1" }),

			Tool.From([Description("Rate a song")](string song, string rating)
			=> "Rated!"),
		]);
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

		services.AddTransient<IAgent, BedrockAgent>();
	});
