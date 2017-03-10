using Microsoft.Spatial;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;
using CommonMark;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Configuration;

public static void Run(string msg, TraceWriter log)
{
    log.Info($"Doc Parser function began execution at: {DateTime.Now}");
    
    string azureQueueConnString =  ConfigurationManager.AppSettings["AzureQueueConnString"];

    string fileName = string.Empty;

    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(azureQueueConnString);
    CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
 
    CloudQueue queue = queueClient.GetQueueReference("docqueue");

    List<AzureDoc> docs = new List<AzureDoc>();

    while (true)
    {
        // Get the next message
        CloudQueueMessage inputMessage = queue.GetMessage();
 
        if (inputMessage == null)
        {
            log.Info("No more messages");

            break;
        }

        try
        {
             string fileContent = inputMessage.AsString;
            //skip docs with this in as they contain no content
            if (fileContent.Contains("redirect_url: ") == false)
            {
                AzureDoc azDoc = new AzureDoc()
                {
                    Content = "",
                    Description = "",
                    FileName = "",
                    Keywords = new List<string>(),
                    LastUpdated = DateTime.Now,
                    Title = "",
                    Url = ""
                };

               

                //this means that the contents sit on the file store not on the queue
                if (fileContent.StartsWith("FILEPROCESS: "))
                {
                    fileName = fileContent.Substring(13, fileContent.Length - 13);
                    var folder = Environment.ExpandEnvironmentVariables(@"%HOME%\data\temp-docs\unzippeddocs");
                    fileContent = File.ReadAllText(Path.Combine(folder, fileName));
                    azDoc.FileName = fileName;
                }

                Regex rDescription = new Regex("description: ([^\"].*)");
                foreach (Match m in rDescription.Matches(fileContent))
                {
                    azDoc.Description = m.ToString().Substring(12, m.Length - 13);
                    break;
                }

                Regex rTitle = new Regex("(title: ).*");
                foreach (Match m in rTitle.Matches(fileContent))
                {

                    azDoc.Title = m.ToString().Substring(7, m.Length - 8);
                    break;
                }

                Regex rFileName = new Regex("FILENAME: ([^\"].*)");
                foreach (Match m in rFileName.Matches(fileContent))
                {
                    fileName = m.ToString().Substring(10, m.Length - 11);
                    break;
                }

                //on some of the docs there was an issue parsing the datetime
                Regex rMsDate = new Regex("ms.date: ([^\"].*)");
                foreach (Match m in rMsDate.Matches(fileContent))
                {
                    try
                    {
                        azDoc.LastUpdated = Convert.ToDateTime(m.ToString().Replace("ms.date: ", ""));
                    }
                    catch (Exception ex)
                    {
                        log.Info("Issue converting ms.date for " + azDoc.FileName);
                        log.Info("error: " + ex.Message);
                    }
                    break;

                }

                //using the CommonMark.NET library to convert markdown to html for processing - https://www.nuget.org/packages/CommonMark.NET/
                var result = CommonMarkConverter.Convert(fileContent);
                azDoc.Content = StripHtmlTags(result);
                azDoc.Content = azDoc.Content.Replace("\r\n", " ");
                
                var fileNameBytes = System.Text.Encoding.UTF8.GetBytes(fileName);
                azDoc.FileName = System.Convert.ToBase64String(fileNameBytes);

                int lastIndexOfDir = fileName.LastIndexOf('#');

                fileName = fileName.Replace("azure-docs-master#articles#", "");
                fileName = fileName.Replace(".md", "");
                fileName = fileName.Replace("#", "/");

                azDoc.Url = fileName;

                azDoc.Title = azDoc.Title.Replace("pageTitle=\"", "");

                azDoc.Title = azDoc.Title.Replace("\"", "");
                azDoc.Title = azDoc.Title.Replace("| Microsoft Docs", "");
                azDoc.Title = azDoc.Title.Replace("| Microsoft Doc", "");

                docs.Add(azDoc);

                queue.DeleteMessage(inputMessage);
            }

        }
        catch (Exception ex)
        {
            log.Info(ex.Message);
            log.Info(ex.StackTrace);
            

        }
    }

    log.Info("Processed " + docs.Count + " docs");

    if(docs.Count > 0)
    {
        //uses cognitive service api to extract keywords from descriptions
        GetKeywords(docs, log).Wait();

        string searchServiceName = ConfigurationManager.AppSettings["AzureSearchSvcName"];

        string apiKey = ConfigurationManager.AppSettings["AzureSearchApiKey"];

        string indexName = string.Format("azure-docs-{0}", DateTime.Now.ToString("yyyyMM"));

        SearchServiceClient serviceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));

        DeleteAzureDocsIndexIfExists(serviceClient, indexName);

        CreateAzureDocsIndex(serviceClient, indexName);
        ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(indexName);

        UploadDocuments(indexClient, docs, log);
    }
}

//Upload documents in batches for better performance
private static void UploadDocuments(ISearchIndexClient indexClient, List<AzureDoc> docs, TraceWriter log)
{
    try
    {
        int count = docs.Count;
 
        int batches = count / 1000;

        for (var x = 0; x <= batches; x++)
        {
            int range = 999;
            if (x == batches)
            {
                range = count - (x * 1000);
            }

            var batch = IndexBatch.Upload(docs.GetRange(x * 1000, range));
 
            indexClient.Documents.Index(batch);
        }
    }
    catch (IndexBatchException e)
    {
        log.Info(e.Message);
    }
}


public static string StripHtmlTags(string html)
{
    if (String.IsNullOrEmpty(html)) 
    {
        return string.Empty;
    }

    //using the HtmlAgilityPack - https://www.nuget.org/packages/HtmlAgilityPack
    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
    
    doc.LoadHtml(html);
    
    return WebUtility.HtmlDecode(doc.DocumentNode.InnerText);
}

static async Task GetKeywords(List<AzureDoc> allDocs, TraceWriter log)
{
    try
    {
        int count = allDocs.Count;
        int batches = count / 1000;

        for (var x = 0; x <= batches; x++)
        {
            int range = 999;
            if (x == batches)
            {
                range = count - (x * 1000);
            }

            var docs = allDocs.GetRange(x * 1000, range);

            string baseUrl = "https://westus.api.cognitive.microsoft.com/";
            
            string accountKey =  ConfigurationManager.AppSettings["CognitiveServiceAPIKey"];;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", accountKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //builds the request
                string request = "{\"documents\":[";
                
                bool first = true;
                
                foreach (var azDoc in docs)
                {
                    if (String.IsNullOrWhiteSpace(azDoc.Description) == false)
                    {
                        string description = azDoc.Description.Replace("\\-", "-");
                        if (first)
                        {
                            request = request + "{\"id\":\"" + azDoc.FileName + "\",\"text\":\"" + description + "\"}";
                            first = false;
                        }
                        else
                        {
                            request = request + ",{\"id\":\"" + azDoc.FileName + "\",\"text\":\"" + description + "\"}";
                        }
                    }
                }

                request += "]}";

                log.Info("batch ready");

                // Detect key phrases: 
                var uri = "text/analytics/v2.0/keyPhrases";
                
                // Request body. Insert your text data here in JSON format.
                byte[] byteData = Encoding.UTF8.GetBytes(request);

                var response = await CallEndpoint(client, uri, byteData);

                dynamic dyn = JObject.Parse(response);

                foreach (var keyword in dyn.documents)
                {
                    AzureDoc doc = docs.First(s => s.FileName == (string)keyword.id);
                    if (doc == null)
                    {
                        continue;
                    }
                    else
                    {
                        doc.Keywords = keyword.keyPhrases.ToObject<List<string>>();
                    }

                }
            }
        }
    }
    catch (Exception ex)
    {
        log.Info("Error getting keywords - is the text analytics key set correctly?");
        log.Info(ex.Message);
    
    }
}

static async Task<String> CallEndpoint(HttpClient client, string uri, byte[] byteData)
{
    using (var content = new ByteArrayContent(byteData))
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await client.PostAsync(uri, content);
        return await response.Content.ReadAsStringAsync();
    }
}

private static void DeleteAzureDocsIndexIfExists(SearchServiceClient serviceClient, string indexName)
{
    if (serviceClient.Indexes.Exists(indexName))
    {
        serviceClient.Indexes.Delete(indexName);
    }
}

private static void CreateAzureDocsIndex(SearchServiceClient serviceClient, string indexName)
{
    Dictionary<string, double> weights = new Dictionary<string, double>();
    weights.Add("Keywords", 3);
    weights.Add("Title", 15);
    weights.Add("Description", 10);
    weights.Add("Content", 1);

    var definition = new Index()
    {
        Name = indexName,
        Fields = new[]
        {
            new Field("FileName", DataType.String)                      {IsKey = true, IsRetrievable = true, IsSearchable=false },
            new Field("Content", DataType.String)                       { IsSearchable = true, IsRetrievable=true },
            new Field("Keywords", DataType.Collection(DataType.String)) { IsSearchable = true, IsRetrievable=true, Analyzer=AnalyzerName.Keyword },
            new Field("Description", DataType.String)                   { IsSearchable = true, IsRetrievable = true },
            new Field("Title", DataType.String)                         { IsSearchable = true, IsRetrievable = true },
            new Field("Url", DataType.String)                           { IsSearchable = false, IsRetrievable = true },
            new Field("LastUpdated", DataType.DateTimeOffset)           { IsFilterable = true, IsRetrievable = true }
        },
        ScoringProfiles = new List<ScoringProfile>()
        {
            new ScoringProfile("booster", new TextWeights(weights))
        }
    };

    serviceClient.Indexes.Create(definition);
}


public class AzureDoc
{
    public string FileName { get; set; }

    public string Url { get; set; }

    public string Content { get; set; }

    public string Description { get; set; }

    public DateTime LastUpdated { get; set; }

    public string Title { get; set; }

    public List<string> Keywords { get; set; }
}