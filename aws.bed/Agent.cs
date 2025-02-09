using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace aws.bed
{
	public class Agent
	{
		//taken from https://docs.anthropic.com/en/docs/build-with-claude/tool-use#chain-of-thought-tool-use
		static string chainOfThoughPrompt = @"Answer the user's request using relevant tools (if they are available). 
Before calling a tool, do some analysis within <thinking></thinking> tags. 
First, think about which of the provided tools is the relevant tool to answer the user's request. 
Second, go through each of the required parameters of the relevant tool and determine if the user has directly provided or given enough information to infer a value. 
When deciding if the parameter can be inferred, carefully consider all the context including the return values from other tools to see if it supports optaining a specific value.
If all of the required parameters are present or can be reasonably inferred, close the thinking tag and proceed with the tool call.
BUT, if one of the values for a required parameter is missing, DO NOT invoke the function (not even with fillers for the missing params) and instead, ask the user to provide the missing parameters. 
DO NOT ask for more information on optional parameters if it is not provided.
----
";
		private readonly IAmazonBedrockRuntime bedrock;
		private readonly ILogger<Agent> logger;

		public Agent(IAmazonBedrockRuntime bedrock, ILogger<Agent> logger)
		{
			this.bedrock = bedrock;
			this.logger = logger;
		}

		public async Task<List<Message>> Do(string task, List<Tool> tools)
		{
			var taskMessage = ConversationRole.User.Says(chainOfThoughPrompt + task);
			var messages = new List<Message> { taskMessage };
			logger.LogInformation("{Role}: {Text}", taskMessage.Role, taskMessage.Text());

			bool keepConversing = true;
			while (keepConversing)
			{
				var response = await bedrock.ConverseAsync(new ConverseRequest
				{
					ModelId = "anthropic.claude-3-sonnet-20240229-v1:0",
					Messages = messages,
					ToolConfig = tools.GetConfig(),
					InferenceConfig = new InferenceConfiguration() { Temperature = 0.0F }
				});

				var responseMessage = response.Output.Message;
				messages.Add(responseMessage);

				var text = responseMessage.Text();
				if (!string.IsNullOrWhiteSpace(text))
				{
					logger.LogInformation("{Role}: {Text}", responseMessage.Role, text);
				}

				if (response.StopReason == StopReason.Tool_use)
				{
					var toolUse = responseMessage.ToolUse();
					logger.LogInformation("{Tool}: Invoking {ToolUse}...", toolUse.Name, toolUse.ToolUseId);

					var toolResult = tools.Use(toolUse);

					messages.Add(ConversationRole.User.Says(toolResult));
				}
				else
				{
					keepConversing = false;
				}
			}

			return messages;
		}


		public class Tool
		{
			public string Name { get; init; }

			private Delegate Delegate { get; init; }

			private Tool(string name, Delegate tool)
			{
				this.Name = name;
				this.Delegate = tool;
			}

			public static Tool From(Delegate tool, [CallerArgumentExpression("tool")] string toolArgumentExpression = "")
			{
				string toolName;
				if (toolArgumentExpression.Contains(' ') || toolArgumentExpression.Contains('.'))
				{
					var displayName = tool.GetMethodInfo().GetCustomAttribute<DescriptionAttribute>()?.Description;
					toolName = Regex.Replace(displayName!, @"[^a-zA-Z0-9]+(.)", m => m.Groups[1].Value.ToUpper());
				}
				else
				{
					toolName = toolArgumentExpression;
				}

				return new Tool(toolName, tool);
			}

			public Amazon.BedrockRuntime.Model.Tool GetDefinition()
			{
				var method = this.Delegate.GetMethodInfo();
				var methodDescription = method.GetCustomAttributes<DescriptionAttribute>().SingleOrDefault()?.Description ?? this.Name;
				var methodParameters = method.GetParameters();
				var toolPropertiesDictionary = methodParameters.ToDictionary(p => p.Name ?? string.Empty, p => new
				{
					Type = p.ParameterType.Name.ToLowerInvariant() switch
					{
						"string" => "string",
						string type => throw new ArgumentOutOfRangeException($"'{type}' parameters are not supported by bedrock json yet."),
					},
					Description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? p.Name,
					Required = p.GetCustomAttribute<RequiredAttribute>() != null,
				});

				return new Amazon.BedrockRuntime.Model.Tool
				{
					ToolSpec = new ToolSpecification
					{
						Name = this.Name,
						Description = methodDescription,
						InputSchema = new ToolInputSchema
						{
							Json = Amazon.Runtime.Documents.Document.FromObject(new
							{
								type = "object",
								properties = toolPropertiesDictionary.ToDictionary(p => p.Key, p => new
								{
									type = p.Value.Type,
									description = p.Value.Description,
								}),
								required = toolPropertiesDictionary.Where(kvp => kvp.Value.Required).Select(kvp => kvp.Key).ToArray(),
							}),
						},
					}
				};
			}

			public ToolResultBlock Use(ToolUseBlock toolUse)
			{
				var inputs = toolUse.Input.AsDictionary();

				var method = this.Delegate.GetMethodInfo();
				var parameters = method.GetParameters()
					.Select(p => (object?)(inputs.TryGetValue(p.Name ?? string.Empty, out var value) ? value.AsString() : default))
					.ToArray();

				var result = this.Delegate.DynamicInvoke(parameters);

				return new ToolResultBlock
				{
					ToolUseId = toolUse.ToolUseId,
					Content =
					[
						new ToolResultContentBlock
						{
							Json = Amazon.Runtime.Documents.Document.FromObject(new
							{
								result
							}),
						}
					]
				};
			}
		}
	}

	public static class AgentExtensions
	{
		public static ToolConfiguration GetConfig(this IEnumerable<Agent.Tool> tools) => new ToolConfiguration
		{
			Tools = tools.Select(tool => tool.GetDefinition()).ToList()
		};

		public static ToolResultBlock Use(this IEnumerable<Agent.Tool> tools, ToolUseBlock toolUse)
		{
			var toolToUse = tools.Single(tool => tool.Name == toolUse.Name);
			return toolToUse.Use(toolUse);
		}

		public static Message Says(this ConversationRole role, ContentBlock content) => new() { Role = role, Content = [content] };
		public static Message Says(this ConversationRole role, ToolResultBlock toolResult) => new() { Role = role, Content = [new ContentBlock { ToolResult = toolResult }] };
		public static Message Says(this ConversationRole role, string text) => new() { Role = role, Content = [new ContentBlock { Text = text }] };

		public static string Text(this Message message) => string.Concat(message.Content.Select(c => c.Text));
		public static ToolUseBlock ToolUse(this Message message)
		{
			var toolUses = message.Content.Select(c => c.ToolUse).Where(t => t != null);
			var toolUse = toolUses.Single();
			return toolUse;
		}
	}
}