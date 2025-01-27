# Dotnet Agent Experiments

We are experimenting with differnt agent patterns.

## Microsoft.Extensions.AI

The package is still in preview. There is no official AWS Bedrock implementation. Works fine with OpenAI.

## SemanticKernel

The package seems quite mature.

### OpenAI

Works great. Also with function calling.

### AzureOpenAI 

Works great, Also with function calling.

### AWS Bedrock

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

