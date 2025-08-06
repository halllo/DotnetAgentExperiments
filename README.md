# Dotnet Agent Experiments

We are experimenting with differnt agent patterns.

## AWSSDK.BedrockRuntime

Lets use the native AWSSDK first.

I was unable to find a dotnet example, that uses function calling. I only found this python example: <https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use-examples.html>

I tried to convert it to dotnet, but it throws this exception at runtime:

```
Amazon.BedrockRuntime.Model.ValidationException
  HResult=0x80131500
  Message=The value at toolConfig.tools.0.toolSpec.inputSchema.json.type must be one of the following: object.
  Source=AWSSDK.Core
  StackTrace:
   at Amazon.Runtime.Internal.HttpErrorResponseExceptionHandler.HandleExceptionStream(IRequestContext requestContext, IWebResponseData httpErrorResponse, HttpErrorResponseException exception, Stream responseStream)
   at Amazon.Runtime.Internal.HttpErrorResponseExceptionHandler.<HandleExceptionAsync>d__2.MoveNext()
   at Amazon.Runtime.Internal.ExceptionHandler`1.<HandleAsync>d__6.MoveNext()
   at Amazon.Runtime.Internal.ErrorHandler.<ProcessExceptionAsync>d__8.MoveNext()
   at Amazon.Runtime.Internal.ErrorHandler.<InvokeAsync>d__5`1.MoveNext()
   at Amazon.Runtime.Internal.CallbackHandler.<InvokeAsync>d__9`1.MoveNext()
   at Amazon.Runtime.Internal.Signer.<InvokeAsync>d__1`1.MoveNext()
   at Amazon.Runtime.Internal.EndpointDiscoveryHandler.<InvokeAsync>d__2`1.MoveNext()
   at Amazon.Runtime.Internal.EndpointDiscoveryHandler.<InvokeAsync>d__2`1.MoveNext()
   at Amazon.Runtime.Internal.CredentialsRetriever.<InvokeAsync>d__7`1.MoveNext()
   at Amazon.Runtime.Internal.RetryHandler.<InvokeAsync>d__10`1.MoveNext()
   at Amazon.Runtime.Internal.RetryHandler.<InvokeAsync>d__10`1.MoveNext()
   at Amazon.Runtime.Internal.CallbackHandler.<InvokeAsync>d__9`1.MoveNext()
   at Amazon.Runtime.Internal.CallbackHandler.<InvokeAsync>d__9`1.MoveNext()
   at Amazon.Runtime.Internal.ErrorCallbackHandler.<InvokeAsync>d__5`1.MoveNext()
   at Amazon.Runtime.Internal.MetricsHandler.<InvokeAsync>d__1`1.MoveNext()
   at Program.<<Main>$>d__0.MoveNext() in D:\DotnetAgentExperiments\aws.bed\Program.cs:line 22
```

I asked about [Function calling with Claude on Amazon Bedrock in dotnet](https://stackoverflow.com/questions/79397902/function-calling-with-claude-on-amazon-bedrock-in-dotnet) at stackoverflow.

After I changed the properties of my anonymous object to lowercase, it worked.

```csharp
Json = Amazon.Runtime.Documents.Document.FromObject(new
{
  type = "object",
  properties = new Dictionary<string, object>
  {
    { "sign", new {
      type = "string",
      description = "The call sign for the radio station for which you want the most popular song. Example calls signs are WZPZ and WKRP."
    } }
  },
  required = new string[]
  {
      "sign"
  },
}),
```

I wrapped this into my own function calling abstraction [AgentDo](https://github.com/halllo/AgentDo).

## Microsoft.Extensions.AI

This seems to be the current best practice in dotnet.

Works fine with OpenAI (`Microsoft.Extensions.AI.OpenAI`).

There is also an official AWS Bedrock implementation (`AWSSDK.Extensions.Bedrock.MEAI`). However it does not seem to support tool use or function calling. The `IChatClient` I defined like this...

```csharp
services.AddKeyedSingleton<IChatClient>("awsbedrock", (sp, key) =>
{
    var runtime = new AmazonBedrockRuntimeClient(
        awsAccessKeyId: config["AWSBedrockAccessKeyId"]!,
        awsSecretAccessKey: config["AWSBedrockSecretAccessKey"]!,
        region: Amazon.RegionEndpoint.GetBySystemName(config["AWSBedrockRegion"]!));

    var client = runtime
        .AsIChatClient("anthropic.claude-3-5-sonnet-20240620-v1:0")
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();

    return client;
});
```

..., does not call its tools when I use it like this:

```csharp
var chatClient = serviceScope.ServiceProvider.GetRequiredKeyedService<IChatClient>("awsbedrock");

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
```

I didn't find example code with function calling. Only basic completion, like <https://github.com/TheCodeTraveler/Bedrock-MEAI-Sample/tree/main/src>.

## Microsoft.SemanticKernel

The package seems both mature and legacy at the same time.

### OpenAI

Works great. Also with function calling.

### AzureOpenAI 

Works great, Also with function calling.

### Connectors.Amazon

The connector package [Microsoft.SemanticKernel.Connectors.Amazon](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.Amazon/) is still in alpha. It supports `chatClient.GetChatMessageContentsAsync()` without function calling.

Function calling seems to not be supported. As soon as I add `PromptExecutionSettings` with `FunctionChoiceBehavior.Auto()`, it throws this exception:

```
Microsoft.SemanticKernel.Connectors.Amazon.Core.BedrockChatCompletionClient[0]
      Can't converse with 'anthropic.claude-3-sonnet-20240229-v1:0'. Reason: 1 validation error detected: Value '0' at 'inferenceConfig.maxTokens' failed to satisfy constraint: Member must have value greater than or equal to 1
      Amazon.BedrockRuntime.Model.ValidationException: 1 validation error detected: Value '0' at 'inferenceConfig.maxTokens' failed to satisfy constraint: Member must have value greater than or equal to 1
       ---> Amazon.Runtime.Internal.HttpErrorResponseException: Exception of type 'Amazon.Runtime.Internal.HttpErrorResponseException' was thrown.
         at Amazon.Runtime.HttpWebRequestMessage.ProcessHttpResponseMessage(HttpResponseMessage responseMessage)
         at Amazon.Runtime.HttpWebRequestMessage.GetResponseAsync(CancellationToken cancellationToken)
         at Amazon.Runtime.Internal.HttpHandler`1.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.Unmarshaller.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.ErrorHandler.InvokeAsync[T](IExecutionContext executionContext)
         --- End of inner exception stack trace ---
         at Amazon.Runtime.Internal.HttpErrorResponseExceptionHandler.HandleExceptionStream(IRequestContext requestContext, IWebResponseData httpErrorResponse, HttpErrorResponseException exception, Stream responseStream)
         at Amazon.Runtime.Internal.HttpErrorResponseExceptionHandler.HandleExceptionAsync(IExecutionContext executionContext, HttpErrorResponseException exception)
         at Amazon.Runtime.Internal.ExceptionHandler`1.HandleAsync(IExecutionContext executionContext, Exception exception)
         at Amazon.Runtime.Internal.ErrorHandler.ProcessExceptionAsync(IExecutionContext executionContext, Exception exception)
         at Amazon.Runtime.Internal.ErrorHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.CallbackHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.Signer.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.EndpointDiscoveryHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.EndpointDiscoveryHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.CredentialsRetriever.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.RetryHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.RetryHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.CallbackHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.CallbackHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.ErrorCallbackHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Amazon.Runtime.Internal.MetricsHandler.InvokeAsync[T](IExecutionContext executionContext)
         at Microsoft.SemanticKernel.Connectors.Amazon.Core.BedrockChatCompletionClient.GenerateChatMessageAsync(ChatHistory chatHistory, PromptExecutionSettings executionSettings, Kernel kernel, CancellationToken cancellationToken)
```

The exception does not occur anymore, when I include the max tokens in the `ExtensionData` of the `PromptExecutionSettings`.

```csharp
ExtensionData = new Dictionary<string, object>() {
    { "max_tokens_to_sample", 4096 }
},
```

Now it generates a response. But it does not invoke the `GetWeather()` function.

There seems to be an issue regarding [#9750 .Net Function Calling with Bedrock Claude](https://github.com/microsoft/semantic-kernel/issues/9750). That issue is now closed, but I still dont get function calling working.

There seems to be another issue regarding [#11448 .Net: Support Function calling for Amazon Bedrock (Using Converse APIs)](https://github.com/microsoft/semantic-kernel/issues/11448). The recommendation seems to be to use `IChatClient` instead of `IChatCompletionService`. And I actually got function calling working with like this:

```csharp
var kernel = serviceScope.ServiceProvider.GetRequiredService<Kernel>();
var chatClient = kernel.GetRequiredService<IChatClient>();

List<ChatMessage> chatHistory = [];
chatHistory.Add(new ChatMessage(ChatRole.System, "You are a helpful AI assistant"));
chatHistory.Add(new ChatMessage(ChatRole.User, "Do I need an umbrella?"));

var invocation = chatClient.GetStreamingResponseAsync(
    messages: chatHistory,
    options: new()
    {
        Temperature = 0f,
        Tools = [AIFunctionFactory.Create([Description("Get the current weather.")]() => kernel.GetRequiredService<WeatherInformation>().GetWeather())]
    });
await foreach (var update in invocation)
{
    Console.Write(update);
}
```

However it feels less well integrated:

1. `ChatHistory` is not updated automatically, but messages need to be maintained manually.
2. Tools are not picked up automatically from plugins but have to be provided again.

There seems to be interoperability method available with `chatClient.AsChatCompletionService();`, but this breaks function calling again.

It seems that with `IChatCompletionService` my tools / plugins do not get picked up when used with AWS Bedrock.

Bedrock is also not on the offical list of function calling support:

<https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/function-calling/function-choice-behaviors?pivots=programming-language-csharp#supported-ai-connectors>

### Adapter by Johnny Z

According to [AWS Bedrock anthropic claude tool call integration with microsoft semantic kernel](https://dev.to/stormhub/aws-bedrock-anthropic-claude-tool-call-integration-with-microsoft-semantic-kernel-29g3) a custom implementation can adapt the AWSSDK.BedrockRuntime IChatClient, like [resources/2025-04-02/ConsoleApp/ConsoleApp/AnthropicChatClient.cs](https://github.com/StormHub/stormhub/blob/main/resources/2025-04-02/ConsoleApp/ConsoleApp/AnthropicChatClient.cs). The implementation reminds me of [AgentDo](https://github.com/halllo/AgentDo), but seems more comprehensive.

It is set up like this:

```csharp
services.AddKeyedSingleton<IChatCompletionService>("anthropic.adapter", (sp, key) =>
{
    var runtime = sp.GetRequiredService<IAmazonBedrockRuntime>();
    IChatClient client = new AnthropicChatClient(runtime, "anthropic.claude-3-5-sonnet-20240620-v1:0");
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    IChatCompletionService chatCompletionService = client
        .AsBuilder()
        .UseFunctionInvocation()
        .Build()
        .AsChatCompletionService();
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    return chatCompletionService;
});
```

It successfully invokes my tools / plugins. This seems to be the best way to get function calling working with AWS Bedrock and SK.
