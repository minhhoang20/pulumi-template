using Pulumi;
using Authorization = Pulumi.AzureNative.Authorization;
using Resources = Pulumi.AzureNative.Resources;
using Storage = Pulumi.AzureNative.Storage;
using Sql = Pulumi.AzureNative.Sql;
using KeyVault = Pulumi.AzureNative.KeyVault;
using Web = Pulumi.AzureNative.Web;
using Network = Pulumi.AzureNative.Network;
using System.Diagnostics;

namespace PulumiTemplate
{
    public class TemplateStack : Stack
    {
        public const string ProjectName = "plm-template";
        public const string PlainProjectName = "plmtemplate";
        public const string Location = "centralus";

        public string EnvironmentName { get; set; }
        public string DeploymentName { get; set; }
        public string PlainDeploymentName { get; set; }

        public TemplateStack()
        {
            var config = new Config();
            string environmentName = config.Require("EnvironmentName");
            this.EnvironmentName = environmentName;
            string environmentSuffix = !string.IsNullOrEmpty(environmentName) ? $"-{environmentName}" : string.Empty;
            this.DeploymentName = $"{ProjectName}{environmentSuffix}";
            this.PlainDeploymentName = $"{PlainProjectName}{this.EnvironmentName}";

            var subscriptionOutput = Output.Create(Authorization.GetClientConfig.InvokeAsync());
            Output.All(subscriptionOutput).Apply(resolveds =>
            {
                SetupResources(resolveds[0]);
                return resolveds[0];
            });           
        }

        public void SetupResources(Authorization.GetClientConfigResult clientConfig)
        {
            if (clientConfig == null)
                throw new ArgumentNullException(nameof(clientConfig));

            string subscriptionId = clientConfig.SubscriptionId;
            string tenantId = clientConfig.TenantId;
            string primaryUserSid = clientConfig.ObjectId;

            string resourceGroupName = $"{this.DeploymentName}-rg";
            var resourceGroup = new Resources.ResourceGroup("rg", new Resources.ResourceGroupArgs
            {
                ResourceGroupName = resourceGroupName,
                Location = Location
            });

            string storageAccountName = $"{this.PlainDeploymentName}sa";
            var storageAccount = new Storage.StorageAccount("sa", new Storage.StorageAccountArgs
            {
                AccountName = storageAccountName,
                ResourceGroupName = resourceGroup.Name,
                Location = Location,
                Sku = new Storage.Inputs.SkuArgs
                {
                    Name = Storage.SkuName.Standard_LRS,
                },
                Kind = Storage.Kind.StorageV2,
            });

            string sqlServerName = $"{this.DeploymentName}-sqlsvr";
            var sqlServer = new Sql.Server("sqlsvr", new Sql.ServerArgs
            {
                ServerName = sqlServerName,
                AdministratorLogin = "sysadmin",
                AdministratorLoginPassword = "@dm!nP0w3r",
                Administrators = new Sql.Inputs.ServerExternalAdministratorArgs
                {
                    AzureADOnlyAuthentication = true,
                    Login = "luuminh20@gmail.com",
                    PrincipalType = "User",
                    Sid = primaryUserSid,
                    TenantId = tenantId,
                },
                Location = Location,
                ResourceGroupName = resourceGroup.Name
            });

            string sqlDbName = $"{this.DeploymentName}-sqldb";
            var sqlDatabase = new Sql.Database("sqldb", new Sql.DatabaseArgs
            {
                DatabaseName = sqlDbName,
                Location = Location,
                ResourceGroupName = resourceGroup.Name,
                ServerName = sqlServer.Name,
                Sku = new Sql.Inputs.SkuArgs
                {
                    Name = "S0"
                }
            }, new CustomResourceOptions
            {
                DependsOn = { sqlServer }
            });

            string keyVaultName = $"{this.DeploymentName}-kv";
            var keyVault = new KeyVault.Vault("kv", new KeyVault.VaultArgs
            {
                VaultName = keyVaultName,
                Location = Location,
                ResourceGroupName = resourceGroup.Name,
                Properties = new KeyVault.Inputs.VaultPropertiesArgs
                {
                    EnableSoftDelete = true,
                    AccessPolicies =
                    {
                        new KeyVault.Inputs.AccessPolicyEntryArgs
                        {
                            ObjectId = primaryUserSid,
                            Permissions = new KeyVault.Inputs.PermissionsArgs
                            {
                                Certificates =
                                {
                                    "get",
                                    "list",
                                    "delete",
                                    "create",
                                    "import",
                                    "update",
                                    "managecontacts",
                                    "getissuers",
                                    "listissuers",
                                    "setissuers",
                                    "deleteissuers",
                                    "manageissuers",
                                    "recover",
                                    "purge",
                                },
                                Keys =
                                {
                                    "encrypt",
                                    "decrypt",
                                    "wrapKey",
                                    "unwrapKey",
                                    "sign",
                                    "verify",
                                    "get",
                                    "list",
                                    "create",
                                    "update",
                                    "import",
                                    "delete",
                                    "backup",
                                    "restore",
                                    "recover",
                                    "purge",
                                },
                                Secrets =
                                {
                                    "get",
                                    "list",
                                    "set",
                                    "delete",
                                    "backup",
                                    "restore",
                                    "recover",
                                    "purge",
                                },
                            },
                            TenantId = tenantId,
                        },
                    },
                    EnabledForDeployment = true,
                    EnabledForDiskEncryption = true,
                    EnabledForTemplateDeployment = true,
                    Sku = new KeyVault.Inputs.SkuArgs
                    {
                        Family = "A",
                        Name = KeyVault.SkuName.Standard
                    },
                    TenantId = tenantId,
                }
            });

            string appServicePlanName = $"{this.DeploymentName}-asp";
            var appServicePlan = new Web.AppServicePlan("asp", new Web.AppServicePlanArgs
            {
                Name = appServicePlanName,
                Kind = "app",
                Location = Location,
                ResourceGroupName = resourceGroup.Name,
                Sku = new Web.Inputs.SkuDescriptionArgs
                {
                    Capacity = 1,
                    Family = "B",
                    Name = "S0",
                    Size = "S0",
                    Tier = "Basic",
                },
            });

            string appName = $"{this.DeploymentName}-app";
            var webApp = new Web.WebApp("app", new Web.WebAppArgs
            {
                Name = appName,
                ResourceGroupName = resourceGroup.Name,
                Location = Location,
                ClientAffinityEnabled = true,
                Enabled = true,
                HttpsOnly = true,
                ServerFarmId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/serverfarms/{appServicePlanName}",
                SiteConfig = new Web.Inputs.SiteConfigArgs
                {
                    Cors = new Web.Inputs.CorsSettingsArgs
                    {
                        AllowedOrigins = "*"
                    }
                }
            }, new CustomResourceOptions
            {
                DependsOn = { appServicePlan }
            });

            string vnetName = $"{this.DeploymentName}-vnet";
            string subnetName = $"{this.DeploymentName}-subnet";
            var vnet = new Network.VirtualNetwork("virtualNetwork", new Network.VirtualNetworkArgs
            {
                VirtualNetworkName = vnetName,
                AddressSpace = new Network.Inputs.AddressSpaceArgs
                {
                    AddressPrefixes =
                {
                    "10.0.0.0/16",
                },
                },
                Location = Location,
                ResourceGroupName = resourceGroup.Name,               
                Subnets =
                {
                    new Network.Inputs.SubnetArgs
                    {
                        AddressPrefix = "10.0.0.0/24",
                        Name = subnetName
                    },
                },
            });
        }
    }
}