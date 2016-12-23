# Azure Helper Bot

This is a simple, proof of concept bot that leverages the following Azure services:
*   Azure Bot Service
*   Microsoft Cognitive Service – Text Analytics
*   Microsoft Cognitive Services – LUIS
*   Azure Search
*   Azure Functions

This bot allows a user to ask questions about Azure and responds with targeted documents that should help the user get the answers they need. 

Every month, an Azure function (repocloner) pulls the latest set of Azure documentation down from the AzureDocs public github repo. It extracts out all the documents and another function churns through those and lands them in an Azure Search index.

The bot is built with the Microsoft Cognitive Service LUIS (Language Understanding Intelligent Service). LUIS has been trained to recognize certain phrases to determine the intent. If the intent is detected as a SearchIntent then, with the training, it trys to extract out the search entity from the message. The bot framework sends the user’s message to LUIS and LUIS extracts the detected entity. The bot service then calls Azure Search with that extracted entity and constructs a message back to the user in the chat window.

# How to use

Try the following types of phrases:

*   Tell me about x
*   What is x
*   How do I do x
*   What are my x limits
