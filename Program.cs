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
var codingAssistant = resourceCreator.CreateOpenAIChatAgent(
        openAIClient, 
        "coder",
        """
            You can write code to resolve a task. Just write a codeblock and nothing more. 
            You'll write Python code by default unless C# or dotnet is asked.

            Try to minimize dependencies on libraries, but you can use pygame.

            #IMPORTANT! 
            When using pygame, make sure the last line of code is pygame.quit() and not quit()
            
        """);
        
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

codingAssistant.SendIntroduction("I will write code to resolve a task, but cannot run it", groupChat);
codeExecutor.SendIntroduction("I can run .NET and Python code", groupChat);

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

