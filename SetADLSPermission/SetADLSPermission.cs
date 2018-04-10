using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.DataLake.Store;
using Microsoft.Rest.Azure.Authentication;
using System.Collections.Generic;
using Microsoft.Azure.DataLake.Store.Acl;
using Microsoft.Azure.DataLake.Store.AclTools;
using Microsoft.Rest;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;

namespace SetADLSPermission
{
    public static class Adminaton
    {
        public static string GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

        [FunctionName("SetADLSPermission")]
        public static void Run([TimerTrigger("0 0 0 1 1 *", RunOnStartup = true)]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            var dataLakeStore = GetEnvironmentVariable("dataLakeStore");
            var accountName = $"{dataLakeStore}.azuredatalakestore.net";

            // Function environment variable clientSecret to authenticate
            // var credentials = GetClientCredentials();
            // var adlsClient = AdlsClient.CreateClient(accountName, credentials);

            // Function environment varaible access token to authenticate
            // var token = GetEnvironmentVariable("token");

            // Programmatic key vault access
            var token = GetAzureAccessTokenFromKeyVault();
            var adlsClient = AdlsClient.CreateClient(accountName, token);

            //ModifyAdlsPermission(log, adlsClient, "777");
            ModifyAdlsAcl(log, adlsClient);
        }

        private static string GetAzureAccessTokenFromKeyVault()
        {
            var clientId = GetEnvironmentVariable("clientId");
            var vaultName = GetEnvironmentVariable("vaultName");
            
            // Use Managed Service Identity
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

            // Use environment variable for secret name
            // var secretName = GetEnvironmentVariable("retrievable");
            // var clientSecret = keyVaultClient.GetSecretAsync($"https://{vaultName}.vault.azure.net/secrets/{secretName}")
            //    .GetAwaiter()
            //    .GetResult();

            // List secrets to obtain secret value (assumes single token in vault)
            var vaultUrl = $"https://{vaultName}.vault.azure.net";
            var secrets = keyVaultClient.GetSecretsAsync(vaultUrl)
                .GetAwaiter()
                .GetResult();
            string secretName = null;
            foreach(var secret in secrets)
            {
                secretName = secret.Identifier.Name;
            }
            var vaultSecret = keyVaultClient.GetSecretAsync(vaultUrl, secretName)
                .GetAwaiter()
                .GetResult();

            // delete after retrieval for security (if key vault isn't read-only locked)
            // var deletedClientSecret = keyVaultClient.DeleteSecretAsync(vaultUrl, secretName)
            //    .GetAwaiter()
            //    .GetResult();

            // If the vaultSecret is a clientSecret (not a Bearer token) then generate a Bearer token auth header
            // var authorizationHeader = GetAuthorizationHeaderWithClientCredentials(
            //    clientId,
            //    vaultSecret.Value);

            return $"Bearer {vaultSecret.Value}";
        }

        private static string GetAzureAccessTokenFromEnvVars()
        {
            return GetAuthorizationHeaderWithClientCredentials(
                GetEnvironmentVariable("clientId"),
                GetEnvironmentVariable("clientSecret")
            );
        }

        private static string GetAuthorizationHeaderWithClientCredentials(string clientId, string clientSecret)
        {
            var tenantId = GetEnvironmentVariable("tenantId");
            var loginUri = $"https://login.microsoftonline.com/{tenantId}/oauth2/authorize";

            var authCtx = new AuthenticationContext(loginUri);
            var credential = new ClientCredential(clientId, clientSecret);

            var result = authCtx.AcquireTokenAsync("https://management.core.windows.net/", credential)
                .GetAwaiter()
                .GetResult();

            if (result == null)
                throw new InvalidOperationException("Failed to aquire token");

            return result.CreateAuthorizationHeader();
        }
        
        private static ServiceClientCredentials GetClientCredentials()
        {
            var credential = new ClientCredential(
                GetEnvironmentVariable("clientId"),
                GetEnvironmentVariable("clientSecret")
            );

            var activeDirectoryServiceSettings = new ActiveDirectoryServiceSettings
            {
                AuthenticationEndpoint = ActiveDirectoryServiceSettings.Azure.AuthenticationEndpoint,
                ValidateAuthority = ActiveDirectoryServiceSettings.Azure.ValidateAuthority,
                TokenAudience = ActiveDirectoryServiceSettings.Azure.TokenAudience
            };

            return ApplicationTokenProvider.
                LoginSilentAsync(
                    GetEnvironmentVariable("domain"),
                    credential,
                    activeDirectoryServiceSettings)
                .GetAwaiter()
                .GetResult();
        }

        private static void ModifyAdlsPermission(TraceWriter log, AdlsClient adlsClient, string permission)
        {
            var numDirectories = ModifyAdlsPermissionDirectory("/", adlsClient, permission);
            log.Info($"Set permission to {permission} on {numDirectories} directories");
        }

        private static int ModifyAdlsPermissionDirectory(string path, AdlsClient adlsClient, string permission)
        {
            adlsClient.SetPermission(path, permission);
            return 1;
        }

        private static void ModifyAdlsAcl(TraceWriter log, AdlsClient adlsClient)
        {
            var aclEntry = new List<AclEntry>()
            {
                new AclEntry(AclType.other, null, AclScope.Access, AclAction.All),
                new AclEntry(AclType.other, null, AclScope.Default, AclAction.All)
            };
            adlsClient.ModifyAclEntries("/", aclEntry);
            log.Info($"Set access and default ACL for /");
            /* Function App doesn't support recursive ACL modificaitons -- April 5, 2017 
            var aclEntry = new List<AclEntry>()
            {
                new AclEntry(AclType.other, null, AclScope.Access, AclAction.All)
            };
            var aclProcessorStats = adlsClient.ChangeAcl("/", aclEntry, RequestedAclType.ModifyAcl);
            log.Info($"Set access ACL for {aclProcessorStats.DirectoryProcessed} directories and {aclProcessorStats.FilesProcessed} files");
            aclEntry = new List<AclEntry>()
            {
                new AclEntry(AclType.other, null, AclScope.Default, AclAction.All)
            };
            aclProcessorStats = adlsClient.ChangeAcl("/", aclEntry, RequestedAclType.ModifyAcl);
            log.Info($"Set default ACL for {aclProcessorStats.DirectoryProcessed} directories and {aclProcessorStats.FilesProcessed} files");*/
        }
    }
}
