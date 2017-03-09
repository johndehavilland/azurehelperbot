Login-AzureRmAccount
New-AzureRmResourceGroup -ResourceGroupName "chatbot-azure1" -Location "East US"
New-AzureRmResourceGroupDeployment -ResourceGroupName "chatbot-azure1" -TemplateParameterFile .\azure_deploy_parameters.json -TemplateFile .\azure_deploy.json
cd ..\backend
git init
git add .
git commit -m "init"
git remote add azure "https://azuredochelperapp.scm.azurewebsites.net"
git push azure master