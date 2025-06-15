using AutoGen;
using AutoGen.Core;
using AutoGen.DotnetInteractive;
using AutoGen.DotnetInteractive.Extension;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using dotenv.net;
using Microsoft.DotNet.Interactive;
using OpenAI;

namespace DevTeamDemo.Resources;

public class ResourceCreator
{

    private Uri _azureOpenAIEndpoint;
    private string _azureOpenAIDeploymentName;
    private string _azureOpenAIkey;

    public ResourceCreator()
    {
        DotEnv.Load();
        _azureOpenAIEndpoint = new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));
        _azureOpenAIDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        _azureOpenAIkey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");

    }

        

    public OpenAIClient CreateOpenAIClient()
    {
        var openAIClient = new AzureOpenAIClient(
            _azureOpenAIEndpoint,
            new AzureKeyCredential(_azureOpenAIkey)
            );
        return openAIClient;
    }

    public CompositeKernel CreateCodingKernel()
    {
        var codingKernel = DotnetInteractiveKernelBuilder
            .CreateEmptyInProcessKernelBuilder()
            .AddCSharpKernel(aliases: ["dotnet"])
            .AddPythonKernel(venv: "python3")
            .Build();
        return codingKernel;
    }

    public MiddlewareStreamingAgent<OpenAIChatAgent> CreateOpenAIChatAgent(OpenAIClient client, string agentName, string systemMessage)
    {
        var openAIChatAgent = new OpenAIChatAgent(
            chatClient: client.GetChatClient(_azureOpenAIDeploymentName),
            name: agentName,
            systemMessage: systemMessage
            )
            .RegisterMessageConnector()
            .RegisterPrintMessage();

        return openAIChatAgent;
    }

    public MiddlewareAgent<UserProxyAgent> CreateUserProxyAgent()
    {
        var userProxyAgent = new UserProxyAgent(
            name: "user",
            humanInputMode: HumanInputMode.ALWAYS)
            .RegisterPrintMessage();

        return userProxyAgent;
    }

    public MiddlewareAgent<DefaultReplyAgent> CreateCodeExecutorAgent(CompositeKernel kernel)
    {
        var runner = new DefaultReplyAgent(
            name: "runner",
            defaultReply: "No code available, coder, write code please")
        .RegisterMiddleware(async (msgs, option, agent, ct) =>
        {
            var mostRecentCoderMessage = msgs.LastOrDefault(x => x.From == "developer");
            if (mostRecentCoderMessage == null)
            {
                // No coder message found, fallback to default reply
                return await agent.GenerateReplyAsync(msgs, option, ct);
            }

            string content = mostRecentCoderMessage.GetContent();

            // Helper function to extract and run code asynchronously
            async Task<TextMessage> TryExecuteCodeAsync(string languageTag, string kernelName)
            {
                var codeBlock = mostRecentCoderMessage.ExtractCodeBlock(languageTag, "```");
                if (string.IsNullOrWhiteSpace(codeBlock))
                {
                    return null;
                }

                try
                {
                    // Run the code asynchronously on the kernel for the correct language
                    var result = await kernel.RunSubmitCodeCommandAsync(codeBlock, kernelName);

                    // Limit output length
                    if (result?.Length > 10000)
                        result = result.Substring(0, 10000);

                    var output = $"""
                # [CODE_BLOCK_EXECUTION_RESULT]
                {result}
                """;

                    return new TextMessage(Role.Assistant, output, from: agent.Name);
                }
                catch (Exception ex)
                {
                    // Handle exception without crashing, inform user
                    var errorMsg = $"""
                # [CODE_EXECUTION_ERROR]
                {ex.Message}
                """;
                    return new TextMessage(Role.Assistant, errorMsg, from: agent.Name);
                }
            }

            TextMessage reply = null;

            if (content.Contains("```csharp"))
            {
                reply = await TryExecuteCodeAsync("```csharp", "csharp2");
            }
            else if (content.Contains("```python"))
            {
                reply = await TryExecuteCodeAsync("```python", "python");
            }

            if (reply != null)
            {
                return reply;
            }

            // No code block matched, fallback to default behavior
            return await agent.GenerateReplyAsync(msgs, option, ct);

        })
        .RegisterPrintMessage();

        return runner;
    }


}
