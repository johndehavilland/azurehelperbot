Login-AzureRmAccount
New-AzureRmResourceGroup -ResourceGroupName "chatbot-azure-1" -Location "East US"
New-AzureRmResourceGroupDeployment -ResourceGroupName "chatbot-azure-1" -TemplateParameterFile .\azure_deploy_parameters.json -TemplateFile .\azure_deploy.json
cd ..\backend
git init
git add .
git commit -m "init"
git remote add azure1 "https://jdhazuredochelperapp .scm.azurewebsites.net"
git push azure master