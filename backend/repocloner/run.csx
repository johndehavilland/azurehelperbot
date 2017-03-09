#r "System.IO.Compression"

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Collections.Generic;
using System.Configuration;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"Doc downloader function began execution at: {DateTime.Now}");

    string pathToGitRepo =  ConfigurationManager.AppSettings["AzureDocsRepo"];
    string azureQueueConnString =  ConfigurationManager.AppSettings["AzureQueueConnString"];

     CloudStorageAccount storageAccount = CloudStorageAccount.Parse(azureQueueConnString);
    CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
    
    // Retrieve a reference to a queue.
    CloudQueue queue = queueClient.GetQueueReference("docdl");

    // Create the queue if it doesn't already exist.
    queue.CreateIfNotExists();

    //clear queue from previous items.
    queue.Clear();
  
    var folder = Environment.ExpandEnvironmentVariables(@"%HOME%\data\temp-docs");

    if (Directory.Exists(folder) == false) 
    {
        Directory.CreateDirectory(folder); 
    }

    
    if(File.Exists(folder + "\\docs.zip")) 
    {
        File.Delete(folder + "\\docs.zip");
    }
    
    using (var client = new WebClient())
    {
        client.Headers.Add("user-agent", "Anything");
        client.DownloadFile(
            pathToGitRepo,
            folder + "\\docs.zip");
    }

  
    CloudQueueMessage messageEnd = new CloudQueueMessage("ready");
    
    queue.AddMessage(messageEnd);

    log.Info("repo downloaded - function complete");
}