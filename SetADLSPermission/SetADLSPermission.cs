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

namespace SetADLSPermission
{
    public static class Adminaton
    {
        public static string GetEnvironmentVariable(string name) => System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

        private static string GetAzureAccessToken()
        {
            string tenantId = GetEnvironmentVariable("domain");
            string loginUri = $"https://login.microsoftonline.com/{tenantId}/oauth2/authorize";

            var authCtx = new AuthenticationContext(loginUri);
            var credential = new ClientCredential(
                GetEnvironmentVariable("clientId"),
                GetEnvironmentVariable("clientSecret")
            );

            var result = authCtx.AcquireToken(resource: "https://management.core.windows.net/",
                clientCredential: credential);

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

        [FunctionName("SetADLSPermission")]
        public static void Run([TimerTrigger("0 0 * * * *", RunOnStartup = true)]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            var dataLakeStore = GetEnvironmentVariable("dataLakeStore");
            var accountName = $"{dataLakeStore}.azuredatalakestore.net";

            // var credentials = GetClientCredentials();
            // var adlsClient = AdlsClient.CreateClient(accountName, credentials);

            // var token = GetEnvironmentVariable("token");
            var token = GetAzureAccessToken();
            var adlsClient = AdlsClient.CreateClient(accountName, token);

            var aclEntry = new List<AclEntry>()
            {
                new AclEntry(AclType.other, null, AclScope.Access, AclAction.All)
            };
            var aclProcessorStats = adlsClient.ChangeAcl("/", aclEntry, RequestedAclType.ModifyAcl);
            log.Info($"Set ACL for {aclProcessorStats.DirectoryProcessed} directories and {aclProcessorStats.FilesProcessed} files");
        }
    }
}
