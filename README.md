# Autogen DevTeam Demo

I've created this demo for a customer event where I explained Agentic AI.
This demo is build with Autogen for dotnet.

Use the nuget feed of the daily build to make this work. You'll need at least the 0.2.2 packages of Autogen.
A nuget.config is included in this repo.

Also be aware that this demo executes code on you local machine. Please don't use this in production and make sure you know who is prompting the model. Bad things can happen. You typically want the code execution to run in a sandbox environment. For the python code to succesfully run install the following packages (and you'll need to install Python locally).
- pip install pygame


Try this prompt
- Write me a snake game and run it please


note: pygame might not quit clean, so the demo could hang at the runner waiting for the program to end.
