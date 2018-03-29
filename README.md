# azure-function-set-data-lake-access

This project is inspired by [AWS CloudFormation custom resources](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/template-custom-resources.html). 
With custom resources you can include any resource in your infrastructure templates, even resources not in AWS.
Azure Resource Manager (ARM) templates don't have an analogous resource type.
However, you can come close deploying an Azure Function that is set to run on startup.

This project demonstrates the approach of deploying a function in an ARM template and retrieve Azure credentials using Azure Key Vault.
The particular example uses the Azure function to set the permission of an Azure Data Lake Store that is created by the template.
It is not possible to set file permissions of the Azure Data Lake Store directly in the ARM template, so the Function is used to set permissions.
