param (
    [string]$resourceGroupName,
    [string]$location="East US",
    [string]$appName,
    [string]$botName,
    [string]$searchServiceName,
    [string]$textAnalyticsSvcName
 )
Login-AzureRmAccount
$subscriptionName = (Get-AzureRmSubscription | Out-GridView -Title "Select an Azure Subscription ..." -PassThru).SubscriptionName
Select-AzureRmSubscription -SubscriptionName $subscriptionName
New-AzureRmResourceGroup -ResourceGroupName $resourceGroupName -Location $location
New-AzureRmResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile .\azure_deploy.json -appName $appName -botName $botName -searchServiceName $searchServiceName -cognitiveServiceName $textAnalyticsSvcName
cd ..\backend
git init
git add .
git commit -m "init"
git remote add azure "https://$appName.scm.azurewebsites.net"
git push azure master