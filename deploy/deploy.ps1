#Login-AzureRmAccount
New-AzureRmResourceGroup -ResourceGroupName "chatbot-azure-help" -Location "East US"
New-AzureRmResourceGroupDeployment -ResourceGroupName "chatbot-azure-helper" -TemplateParameterFile .\azure_deploy_parameters.json -TemplateFile .\azure_deploy.json
#cd ..
#git init
##git add .
#git commit -m "init"
#git remote add azure "https://azuredochelper.scm.azurewebsites.net"
#git push azure master