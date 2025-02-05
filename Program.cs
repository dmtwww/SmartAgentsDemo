using AutoGen.Core;
using DevTeamDemo.Resources;

Console.Clear();
Console.WriteLine("--- CREATE RESOURCES ---");
Console.WriteLine("- Read Configuration");
var resourceCreator = new ResourceCreator();
Console.WriteLine("- Create Azure OpenAI Client");
var openAIClient = resourceCreator.CreateOpenAIClient();
Console.WriteLine("- Create Code Execution environment");
var codingKernel = resourceCreator.CreateCodingKernel();


Console.WriteLine("- Create Coding Agent");
var codingAssistant = resourceCreator.CreateOpenAIChatAgent(openAIClient, "coder", "you can write code to resolve a task");
Console.WriteLine("- Create Groupchat Admin");
var groupChatAdmin = resourceCreator.CreateOpenAIChatAgent(openAIClient, "groupadmin", "you manage the groupchat");
Console.WriteLine("- Create Code Executor Agent");
var codeExecutor = resourceCreator.CreateCodeExecutorAgent(codingKernel);
Console.WriteLine("- Create User Proxy Agent");
var userProxy = resourceCreator.CreateUserProxyAgent();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("All done and ready, what do you want me to do?");
var task = Console.ReadLine();

if (task.ToLower().Contains("exit"))
{
    Console.WriteLine("Goodbye!");
    Console.ForegroundColor = ConsoleColor.Gray;
    return;
}

var groupChat = new GroupChat(
    admin: groupChatAdmin,
    members: [
        codingAssistant, 
        codeExecutor, 
        userProxy
    ]);

var chatHistory = new List<IMessage>
        {
            new TextMessage(Role.User, task)
            {
                From = userProxy.Name
            }
        };

await foreach (var message in groupChat.SendAsync(chatHistory, maxRound: 10))
{
    if (message.From == userProxy.Name && message.GetContent().Contains("[GROUPCHAT_TERMINATE]"))
    {
        // Task complete!
        break;
    }
}

Console.ForegroundColor = ConsoleColor.Gray;

