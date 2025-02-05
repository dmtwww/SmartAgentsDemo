using AutoGen;
using AutoGen.Core;
using AutoGen.DotnetInteractive;
using AutoGen.DotnetInteractive.Extension;
using AutoGen.OpenAI;
using AutoGen.OpenAI.Extension;
using Azure.AI.OpenAI;
using Azure.Identity;
using dotenv.net;
using Microsoft.DotNet.Interactive;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace DevTeamDemo.Resources;

public class ResourceCreator
{

    private Uri _azureOpenAIEndpoint;
    private string _azureOpenAIDeploymentName;

    public ResourceCreator()
    {
        DotEnv.Load();
        _azureOpenAIEndpoint = new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));
        _azureOpenAIDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");

    }

    public OpenAIClient CreateOpenAIClient()
    {
        var openAIClient = new AzureOpenAIClient(
            _azureOpenAIEndpoint,
            new AzureCliCredential()
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
            var mostRecentCoderMessage = msgs.LastOrDefault(x => x.From == "coder") ?? throw new Exception("No coder message found");

            if (mostRecentCoderMessage.GetContent().Contains("```csharp"))
            {

                if (mostRecentCoderMessage.ExtractCodeBlock("```csharp", "```") is string code)
                {
                    var result = await kernel.RunSubmitCodeCommandAsync(code, "csharp2");
                    // only keep the first 500 characters
                    if (result.Length > 10000)
                    {
                        result = result.Substring(0, 10000);
                    }
                    result = $"""
                    # [CODE_BLOCK_EXECUTION_RESULT]
                    {result}
                    """;

                    return new TextMessage(Role.Assistant, result, from: agent.Name);
                }
            }

            if (mostRecentCoderMessage.GetContent().Contains("```python"))
            {

                if (mostRecentCoderMessage.ExtractCodeBlock("```python", "```") is string code)
                {
                    var result = await kernel.RunSubmitCodeCommandAsync(code, "python");
                    // only keep the first 500 characters
                    if (result.Length > 10000)
                    {
                        result = result.Substring(0, 10000);
                    }
                    result = $"""
                    # [CODE_BLOCK_EXECUTION_RESULT]
                    {result}
                    """;

                    return new TextMessage(Role.Assistant, result, from: agent.Name);
                }
            }

            return await agent.GenerateReplyAsync(msgs, option, ct);

        })
        .RegisterPrintMessage();
        
        return runner;

    }  

}
