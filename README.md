# Dotnet Agent Experiments

I am experimenting with different agent patterns in dotnet. Each approach lives in its own project and targets the same goal: function/tool calling against Amazon Bedrock (Anthropic Claude), and increasingly OpenAI as well.

## Basic Function Calling

Comparing different options for function calling with Amazon Bedrock.

### AWSSDK.BedrockRuntime (`aws.bed`)

The lowest-level option: call the Bedrock `Converse` API directly through the AWS SDK. Tools are described by hand as a JSON `ToolInputSchema`, and the tool-use / tool-result loop is driven manually. It works and has no extra dependencies, but it is verbose and every bit of plumbing (schema, argument parsing, message bookkeeping) is on you.

### AgentDo (`aws.bed`)

[AgentDo](https://github.com/halllo/AgentDo) wraps the SDK in a small `BedrockAgent` that runs the tool loop for you. Tools are plain C# lambdas with `[Description]`/`[Required]` attributes, so there is no manual schema. It is a pragmatic, Bedrock-focused abstraction that removes most of the boilerplate of the raw SDK approach.

### Microsoft.Extensions.AI + AWSSDK.Extensions.Bedrock.MEAI (`ms.ext.ai`)

Microsoft.Extensions.AI (MEAI) provides the provider-agnostic `IChatClient` abstraction. Combined with `AWSSDK.Extensions.Bedrock.MEAI` (Bedrock) and `Microsoft.Extensions.AI.OpenAI` (OpenAI), the same code drives either provider. `AIFunctionFactory.Create` turns methods into tools and `UseFunctionInvocation()` runs the loop automatically. This is the cleanest of the "plain" options and the foundation everything else builds on.

### Custom Bedrock adapter (`ms.ext.ai.custom`)

A hand-written `IChatClient` for Bedrock (`CustomBedrockChatClient`) based on the adapter described in [Johnny Z's article](https://dev.to/stormhub/aws-bedrock-anthropic-claude-tool-call-integration-with-microsoft-semantic-kernel-29g3). It shows what MEAI's Bedrock connector does under the hood and is useful when you need to customize the Converse mapping (e.g. reasoning, tool formatting) yourself.

### Results

![test results](testresults.png)

## Semantic Kernel (`sem.ker`)

> [!WARNING]
> With the advent of [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (building on MEAI), Semantic Kernel does not have a future!

Semantic Kernel can do function calling with Bedrock, but only via the `IChatClient` path — not the classic `IChatCompletionService`, where tools/plugins are not picked up for Bedrock (see [semantic-kernel#11448](https://github.com/microsoft/semantic-kernel/issues/11448) and the [supported connectors list](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/function-calling/function-choice-behaviors?pivots=programming-language-csharp#supported-ai-connectors)). Even then it feels poorly integrated: chat history and tools must be maintained manually, and the `AsChatCompletionService()` interop bridge breaks function calling again. Given the direction of travel, this is a dead end.

## Microsoft Agent Framework (`ms.af`)

The successor to Semantic Kernel, built on top of MEAI. Any `IChatClient` (OpenAI or Bedrock here) becomes an `AIAgent` via `chatClient.AsAIAgent(new ChatClientAgentOptions { ... })`, so both providers run through one identical code path — instructions, tools and reasoning options all live in the agent's `ChatOptions`.

Conversation state is a first-class concept: an `AgentSession` holds the history, and `agent.SerializeSessionAsync` / `DeserializeSessionAsync` persist and restore it (here to `history.json`), so a conversation survives restarts. The agent wraps the client with its own function-invocation loop, so tools just work, including human-in-the-loop **tool approval**: wrap a tool in `ApprovalRequiredAIFunction`, and instead of executing it the agent returns a `ToolApprovalRequestContent` that you approve or reject and feed back in. Responses stream via `RunStreamingAsync`, exposing reasoning and text content separately. This is the recommended approach going forward.
