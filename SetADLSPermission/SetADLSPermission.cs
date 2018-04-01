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

            // var credentials = GetClientCredentials();
            // var adlsClient = AdlsClient.CreateClient(accountName, credentials);

            // var token = GetEnvironmentVariable("token");
            // var token = GetAzureAccessToken();
            var token = GetAzureAccessTokenFromKeyVault(log);
            var adlsClient = AdlsClient.CreateClient(accountName, token);

            ModifyAdlsPermission(log, adlsClient, "777");
        }

        private static string GetAzureAccessTokenFromKeyVault(TraceWriter log)
        {
            var clientId = GetEnvironmentVariable("clientId");
            var vaultName = GetEnvironmentVariable("vaultName");
            // var retreivable = GetEnvironmentVariable("retrievable");

            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

            var keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

            var vaultUrl = $"https://{vaultName}.vault.azure.net";
            var secrets = keyVaultClient.GetSecretsAsync(vaultUrl)
                .GetAwaiter()
                .GetResult();
            string secretName = null;
            foreach(var secret in secrets)
            {
                secretName = secret.Identifier.Name;
            }
            log.Info($"Found {secretName}");
            var clientSecret = keyVaultClient.DeleteSecretAsync(vaultUrl, secretName)
                .GetAwaiter()
                .GetResult();

            // var clientSecret = keyVaultClient.GetSecretAsync($"https://{vaultName}.vault.azure.net/secrets/{retreivable}")
            //    .GetAwaiter()
            //    .GetResult();

            var token = GetTokenWithClientCredentials(
                clientId,
                clientSecret.Value);

            keyVaultClient.PurgeDeletedSecretAsync(clientSecret.RecoveryId)
                .GetAwaiter()
                .GetResult();
            log.Info("Purged");

            return token;
        }

        private static string GetAzureAccessTokenFromEnvVars()
        {
            return GetTokenWithClientCredentials(
                GetEnvironmentVariable("clientId"),
                GetEnvironmentVariable("clientSecret")
            );
        }

        private static string GetTokenWithClientCredentials(string clientId, string clientSecret)
        {
            string tenantId = GetEnvironmentVariable("tenantId");
            string loginUri = $"https://login.microsoftonline.com/{tenantId}/oauth2/authorize";

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

        private static void ModifyAdlsPermission(TraceWriter log, AdlsClient adlsClient, string permisison)
        {
            adlsClient.SetPermission("/", permisison);
            log.Info($"Set permission to {permisison}");
        }

        private static void ModifyAdlsAcl(TraceWriter log, AdlsClient adlsClient)
        {
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
            aclProcessorStats = adlsClient.ChangeAcl("/", aclEntry, RequestedAclType.SetAcl);
            log.Info($"Set default ACL for {aclProcessorStats.DirectoryProcessed} directories and {aclProcessorStats.FilesProcessed} files");
        }
    }
}
