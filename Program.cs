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
        "developer",
        """
            You can write code to resolve a task based on specification or a feedback provided.

            Always return full code even if there are only small or no changes needed.

            Make sure to check that the game or app area is within the window and does not go outside of it.

            Increase game window if needed.
            
            Just write a codeblock and nothing more. 
            
            You'll write Python code by default unless C# or dotnet is asked.

            Try to minimize dependencies on libraries, but you can use pygame.

            #IMPORTANT! 
            When using pygame, make sure the last line of code is pygame.quit() and not quit()
            
        """);

Console.WriteLine("- Create Specification Agent");
var specificationAssistant = resourceCreator.CreateOpenAIChatAgent(
        openAIClient,
        "architect",
        """
        You are a professional product designer and software architect. Create a clear, consistent, and visually polished specification for a simple game (based on pygame) or application, suitable for demo or prototype purposes.

        Keep it compact but detailed. Focus on clarity, completeness, and internal consistency. Resulting app should be high-quality, consistent, and visually appealing. Do not include sound, localization, or monetization.

        Within the specification for games make sure to include restart functionality, and a way to exit the game.

        Do not ask questions, just write a specification. Use markdown format for the specification. Do not use any code blocks, just write text.
            
        """);

Console.WriteLine("- Create Q&A Agent");
var qaAssistant = resourceCreator.CreateOpenAIChatAgent(
        openAIClient,
        "tester",
        """
        You are a code QA and debugging assistant. Given a codebase produced by a coder agent and a specification document, your job is to:

        Answer questions about how the code works or why something might not be working.

        Compare code behavior to the specification and identify mismatches or bugs.

        Do not provide info about what is good, only what need to be fixed.

        Suggest specific fixes or improvements to make the code meet the intended behavior.

        Be clear, accurate, and focused on helping the user understand and correct issues.

        Do not ask questions, do not write code, just provide feedback and suggestions. Use markdown format for the feedback. 

        Make sure to check that the game or app area is within the window and does not go outside of it.
        
        You should always provide suggestions for improvement, even if the code is correct.
          
        """);

Console.WriteLine("- Create Groupchat Admin");

var groupChatAdmin = resourceCreator.CreateOpenAIChatAgent(
        openAIClient,
        "groupadmin",
        """
        You manage the groupchat. 

        #IMPORTANT! Never put messages to the chat, just manage the control.

        You need to strictly follow the following sequence:

        1. Wait for a task from the user.
        2. Provide the task to the architect.
        3. Wait for the specification from the architect.
        4. Provide the specification to the developer.
        5. Wait for the code from the developer.
        6. Provide the code to the tester.
        7. Wait for the feedback from the tester.
        8. Provide the feedback to the developer.
        9. Wait for the updated code from the developer.
        10. Provide the updated code to the runner.
        11. Wait for the code execution result from the runner (look for CODE_BLOCK_EXECUTION_RESULT or CODE_EXECUTION_ERROR).
        12. Collect feedback and provide this feedback to the developer (GOTO #8).              
            
        """);

Console.WriteLine("- Create Code Executor Agent");
var codeExecutor = resourceCreator.CreateCodeExecutorAgent(codingKernel);
Console.WriteLine("- Create User Proxy Agent");
var userProxy = resourceCreator.CreateUserProxyAgent();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("All done and ready, what do you want me to do? (type 'exit' to quit)");


var groupChat = new GroupChat(
    admin: groupChatAdmin,
    members: [
        codingAssistant,
        specificationAssistant,
        qaAssistant,
        codeExecutor,
        userProxy
    ]);

groupChatAdmin.SendIntroduction("I am the group chat admin and will help manage this conversation.", groupChat);
specificationAssistant.SendIntroduction("I will write a specification for a task", groupChat);
qaAssistant.SendIntroduction(qaAssistant.Name + "I will provide feedback about code", groupChat);
codingAssistant.SendIntroduction("I will write code to resolve a task, but cannot run it", groupChat);
codeExecutor.SendIntroduction("I can run .NET and Python code", groupChat);


while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    var task = Console.ReadLine();
    Console.ForegroundColor = ConsoleColor.Gray;

    if (string.IsNullOrWhiteSpace(task) || task.ToLower().Contains("exit"))
    {
        Console.WriteLine("Goodbye!");
        Console.ForegroundColor = ConsoleColor.Gray;
        break;
    }
        
    var chatHistory = new List<IMessage>
    {
        new TextMessage(Role.User, task)
        {
            From = userProxy.Name
        }
    };
    try
    {
        await foreach (var message in groupChat.SendAsync(chatHistory, maxRound: 10))
        {
            if (message.From == userProxy.Name && message.GetContent().Contains("[GROUPCHAT_TERMINATE]"))
            {
                // Task complete!
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending task: {ex.Message}");
        continue;
    }

    
}
