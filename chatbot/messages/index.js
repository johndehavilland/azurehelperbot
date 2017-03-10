/*-----------------------------------------------------------------------------
This bot interacts with luis.ai to get intents and then queries Azure Search.
-----------------------------------------------------------------------------*/
"use strict";

var builder = require("botbuilder");
var botbuilder_azure = require("botbuilder-azure");
var AzureSearch = require('azure-search');

var useEmulator = (process.env.NODE_ENV == 'development');

var connector = useEmulator ? new builder.ChatConnector() : new botbuilder_azure.BotServiceConnector({
    appId: process.env['MicrosoftAppId'],
    appPassword: process.env['MicrosoftAppPassword'],
    stateEndpoint: process.env['BotStateEndpoint'],
    openIdMetadata: process.env['BotOpenIdMetadata']
});

var searchClient = AzureSearch({
    url: process.env.AzureSearchEndpoint,
    key:process.env.AzureSearchKey
});

var bot = new builder.UniversalBot(connector);

var luisAppId = process.env.LuisAppId;
var luisAPIKey = process.env.LuisAPIKey;
var luisAPIHostName = process.env.LuisAPIHostName || 'api.projectoxford.ai';

const LuisModelUrl = 'https://' + luisAPIHostName + '/luis/v1/application?id=' + luisAppId + '&subscription-key=' + luisAPIKey;

var recognizer = new builder.LuisRecognizer(LuisModelUrl);
var intents = new builder.IntentDialog({ recognizers: [recognizer] })
                    .matches('None', (session, args) => {
                        session.send('Hi! This is the None intent handler. You said: \'%s\'.', session.message.text);
                    })
                    .matches('SearchActivity', [searchActivity, getSearchResults])
                    .matches('LimitActivity', [limitsActivity])
                    .onDefault((session) => {
                        session.send('Sorry, I did not understand \'%s\'.', session.message.text);
                    });

bot.dialog('/', intents);    

if (useEmulator) {
    var restify = require('restify');
    var server = restify.createServer();
    server.listen(3978, function() {
        console.log('bot ready for local testing at http://localhost:3978/api/messages');
    });
    server.post('/api/messages', connector.listen());    
} else {
   var listener = connector.listen();
    var withLogging = function(context, req) {
        listener(context, req);
    }

    module.exports = { default: withLogging }
}

function searchActivity(session, args, next){
    console.log("search activity triggered");

    var searchTerm = builder.EntityRecognizer.findEntity(args.entities, 'SearchTerm');
    
    if (!searchTerm) {
        console.log("searchTerm not recognized: " + args.entities);
        session.send('Hmm, could you rephrase that?');
    } else {

        console.log("entity found: " + searchTerm.entity)
        
        //using the entity query the azure search index
        var searchOptions = { 
                                search: searchTerm.entity, 
                                '$select': 'Title,Url,Description,FileName', 
                                scoringProfile:'booster',
                                searchMode: 'all'
                            };
        
        var dt = new Date();
        var indexName = "azure-docs-" + dt.getFullYear() + ("0" + (dt.getMonth() + 1)).slice(-2);
        
        searchClient.search(indexName, searchOptions, function(err, results) {
                                                            next({ response: results});                            
                                                            if(err){
                                                                console.log("error  " + err);
                                                            }
                                                        });
    }
}

function getSearchResults (session, results) {

    var searchResponse = [];
    var searchResults = results.response;
    
    for(var count = 0; count < 3; count++){
        if (searchResults[count]) {
            var docUrl = "https://docs.microsoft.com/en-us/azure/"+ searchResults[count].Url
            var title = searchResults[count].Title;
            var description = searchResults[count].Description;       
            var fileName = searchResults[count].FileName;
            searchResponse.push({
                url:docUrl, 
                title:title,
                description:description,
                fileName:fileName
            });
        }
    }

    if(searchResponse.length > 0){
        session.send("Found a couple of articles \n\n"
           + "1. [%(resp[0].title)s](%(resp[0].url)s) - %(resp[0].description)s \n\n"
           + "2. [%(resp[1].title)s](%(resp[1].url)s) - %(resp[1].description)s",
           {resp: searchResponse});
    } else {
        session.send("Ooops, seems like I couldn't find anything...try rewording your phrase.");
    }
}

function limitsActivity(session,args,next){
    console.log("limit activity triggered");

    var limitEntity = builder.EntityRecognizer.findEntity(args.entities, 'LimitType');
    
    if(limitEntity){
        console.log("limit entity" + JSON.stringify(limitEntity));
        
        var limits = builder.EntityRecognizer.findBestMatch(limitData, limitEntity.entity);
        
        if(limits && limitData[limits.entity]){
            session.send("See the " + limits.entity+" limits [here](https://docs.microsoft.com/en-us/azure/azure-subscription-service-limits#"+limitData[limits.entity] + ")");
        }
        else{
            session.send("Were you trying to get limit information? I did not recognize the service. Try again or visit our (limits page)[https://azure.microsoft.com/en-us/documentation/articles/azure-subscription-service-limits/]");
        }
    }
    else{
        session.send("Were you trying to get limit information? I did not recognize the service. Try again or visit our (limits page)[https://azure.microsoft.com/en-us/documentation/articles/azure-subscription-service-limits/]");
    }
} 

var limitData ={
   'Active Directory':"active-directory-limits",
'API Management':"api-management-limits",
'App Service':"app-service-limits",
'Application Gateway':"application-gateway-limits",
'Application Insights':"application-insights-limits",
'Automation':"automation-limits",
'Azure Redis Cache':"azure-redis-cache-limits",
'Azure RemoteApp':"azure-remoteapp-limits",
'Backup':"backup-limits",
'Batch':"batch-limits",
'BizTalk Services':"biztalk-services-limits",
'CDN':"cdn-limits",
'Cloud Services':"cloud-services-limits",
'Data Factory':"data-factory-limits",
'Data Lake Analytics':"data-lake-analytics-limits",
'DNS':"dns-limits",
'DocumentDB':"documentdb-limits",
'Event Hubs':"event-hubs-limits",
'IoT Hub':"iot-hub-limits",
'Key Vault':"key-vault-limits",
'Media Services':"media-services-limits",
'Mobile Engagement':"mobile-engagement-limits",
'Mobile Services':"mobile-services-limits",
'Monitoring':"monitoring-limits",
'Multi-Factor Authentication':"multi-factor-authentication",
'Networking':"networking-limits",
'Notification Hub Service':"notification-hub-service-limits",
'Operational Insights':"operational-insights-limits",
'Resource Group':"resource-group-limits",
'Scheduler':"scheduler-limits",
'Search':"search-limits",
'Service Bus':"service-bus-limits",
'Site Recovery':"site-recovery-limits",
'SQL Database':"sql-database-limits",
'Storage':"storage-limits",
'StorSimple System':"storsimple-system-limits",
'Stream Analytics':"stream-analytics-limits",
'Subscription':"subscription-limits",
'Traffic Manager':"traffic-manager-limits",
'Virtual Machines':"virtual-machines-limits",
'Virtual Machine Scale Sets':"virtual-machine-scale-sets-limits"
};

