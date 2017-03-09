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

public static void Run(string msg, TraceWriter log)
{
    log.Info($"Doc downloader function began execution at: {DateTime.Now}");

    string pathToGitRepo =  ConfigurationManager.AppSettings["AzureDocsRepo"];
    string azureQueueConnString =  ConfigurationManager.AppSettings["AzureQueueConnString"];
  
    var folder = Environment.ExpandEnvironmentVariables(@"%HOME%\data\temp-docs");


    if (Directory.Exists(folder + "\\unzippeddocs") == false) 
    {
        Directory.CreateDirectory(folder + "\\unzippeddocs"); 
    }
    else
    {
        Directory.Delete(folder + "\\unzippeddocs", true);
        Directory.CreateDirectory(folder + "\\unzippeddocs"); 
    }


    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(azureQueueConnString);
    CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
    
    // Retrieve a reference to a queue.
    CloudQueue queue = queueClient.GetQueueReference("docqueue");

    // Create the queue if it doesn't already exist.
    queue.CreateIfNotExists();

    //clear queue from previous items.
    queue.Clear();
    log.Info("queue ready");
    
    string extractPath = folder + "\\unzippeddocs";
    
    using (ZipArchive archive = ZipFile.OpenRead(folder + "\\docs.zip"))
    {
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
           
            string fileName = entry.FullName.Replace("/", "#");
            try
            {
                if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && fileName.Contains("-TOC")==false && fileName.Contains("-README")==false)
                {
                        using (var stream = entry.Open()){
                            using (var reader = new StreamReader(stream)) {
                                string fileContents = reader.ReadToEnd();
                                //push path to queue
                                CloudQueueMessage message = new CloudQueueMessage("FILENAME: " + fileName + Environment.NewLine + fileContents);
                                queue.AddMessage(message);
                            }
                        }
                }
            }
            catch(Exception e){
                log.Info(e.Message);
                log.Info(fileName);
                //if the file is too big to go on the queue then extract to a flat file and add the FILEPROCESS tag
                //this way the consuming function will know to look for these on the file system.
                entry.ExtractToFile(Path.Combine(extractPath, fileName));
                CloudQueueMessage message = new CloudQueueMessage("FILEPROCESS: " + fileName);
                queue.AddMessage(message);
            }
        }
    } 

    CloudQueue queueEnd = queueClient.GetQueueReference("docnotify");
    
    // Create the queue if it doesn't already exist.
    queueEnd.CreateIfNotExists();
    
    CloudQueueMessage messageEnd = new CloudQueueMessage("ready");
    
    queueEnd.AddMessage(messageEnd);

    log.Info("docs extracted - function complete");    
}