# azure-function-set-data-lake-access

This project is inspired by [AWS CloudFormation custom resources](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/template-custom-resources.html). 
With custom resources you can include any resource in your infrastructure templates, even resources not in AWS.
Azure Resource Manager (ARM) templates don't have an analogous resource type.
However, you can come close deploying an Azure Function that is set to run on startup.

This project demonstrates the approach of deploying a function in an ARM template and retrieve Azure credentials using Azure Key Vault.
The particular example uses the Azure function to set the permission of an Azure Data Lake Store that is created by the template.
It is not possible to set file permissions of the Azure Data Lake Store directly in the ARM template, so the Function is used to set permissions.


## Repo Organization

- `templates`: ARM templates to deploy Azure Functions capable of modifying resources created by the template (Data Lake Store permissions in the example)
    - `function-env-vars`: Includes arm templates for creating a key vault (`key-vault-template.json`), deploying a data lake and function to modify its permissions (`arm-template.json`), and a parent template to dynamically set the secureString parameter values from Azure Key Vault without a separate parameters file (`vault-parameters-template.json`).  Use the function-env-var branch for a ready to use version.
![azurecustomresource1](https://user-images.githubusercontent.com/3911650/38169596-858402f4-352b-11e8-96b7-02ef029bd00a.png)
	- `programmatic-key-vault-access`: Includes arm templates for deploying a data lake, function with a managed service identity to modify its permissions, and key vault to store the secret that the function programmatically accesses (not via an environment variable)
- `SetADLSPermission`: Azure Function project to set the permission of the Data Lake Store. This project is deployed by `templates/arm-template.json`.

## Notes

A consumption plan template is also included `arm-template-consumption.json`. But consumption plans don't support `alwaysOn` so you need to use a frequent schedule or external call to wake the function to have it run even with `RunOnStartup = true`.
The app service plan template requires at least a basic tier because free and shared tiers don't support `alwaysOn`. Basic tier costs $0.075/hour at time of writing.
