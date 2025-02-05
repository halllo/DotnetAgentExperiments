# Dotnet Agent Experiments

We are experimenting with differnt agent patterns.

## Microsoft.Extensions.AI

The package is still in preview. There is no official AWS Bedrock implementation. Works fine with OpenAI.

## Microsoft.SemanticKernel

The package seems quite mature.

### OpenAI

Works great. Also with function calling.

### AzureOpenAI 

Works great, Also with function calling.

### Connectors.Amazon

The connector package is still in alpha. It supports `chatClient.GetChatMessageContentsAsync()` without function calling.

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

There seems to be an open issue regarding [#9750 .Net Function Calling with Bedrock Claude](https://github.com/microsoft/semantic-kernel/issues/9750).

## AWSSDK.BedrockRuntime

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
