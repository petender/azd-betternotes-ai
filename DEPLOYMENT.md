# Azure Developer CLI Deployment Guide

This guide will walk you through deploying the BetterNotes application to Azure using Azure Developer CLI (azd).

## Prerequisites

1. **Install Azure Developer CLI (azd)**
   ```bash
   # Windows (PowerShell)
   winget install microsoft.azd
   
   # Or download from: https://aka.ms/install-azd
   ```

2. **Install Azure CLI** (if not already installed)
   ```bash
   winget install microsoft.azurecli
   ```

3. **Azure Subscription**
   - Active Azure subscription
   - Contributor or Owner permissions

## Deployment Steps

### 1. Initialize the Environment

```powershell
# Login to Azure
az login

# Login to Azure Developer CLI
azd auth login

# Initialize the environment (first time only)
azd init
```

When prompted:
- **Environment name**: Enter a unique name (e.g., `betternotes-dev`)
- **Azure subscription**: Select your subscription
- **Azure location**: Choose a location (e.g., `eastus`)

### 2. Provision Azure Resources

This will create all necessary Azure resources (App Service, Document Intelligence, etc.):

```powershell
azd provision
```

This command will:
- Create a resource group
- Deploy App Service with System-assigned Managed Identity
- Deploy Azure Document Intelligence (Form Recognizer)
- Configure role assignments for Managed Identity
- Set up Application Insights for monitoring
- Configure all app settings

### 3. Deploy the Application

```powershell
azd deploy
```

This will:
- Build the .NET application
- Package the application
- Deploy to Azure App Service

### 4. Access Your Application

After deployment completes, you can access your application:

```powershell
# Get the application URL
azd show

# Or open directly in browser
azd browse
```

## Infrastructure Overview

The deployment creates the following resources:

### Resource Group
- **Name Pattern**: `rg-{environmentName}`
- **Contains**: All resources for the application

### App Service
- **Name Pattern**: `app-web-{uniqueString}`
- **Runtime**: .NET 8.0 on Linux
- **Plan**: Basic B1 (can be customized)
- **Features**:
  - System-assigned Managed Identity enabled
  - HTTPS only
  - Application Insights integration
  - Configured with Document Intelligence endpoint

### Azure Document Intelligence
- **Name Pattern**: `cog-{uniqueString}`
- **SKU**: S0 (Standard)
- **Role Assignment**: App Service's Managed Identity has "Cognitive Services User" role

### Monitoring
- **Log Analytics Workspace**: For centralized logging
- **Application Insights**: For application monitoring and diagnostics

## Configuration

### Environment Variables

The following settings are automatically configured:

| Setting | Description | Set By |
|---------|-------------|--------|
| `AzureAI__Endpoint` | Document Intelligence endpoint | azd provision |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights connection | azd provision |

### Customizing the Deployment

Edit `infra/main.bicep` to customize:

1. **App Service Plan SKU**:
   ```bicep
   sku: {
     name: 'B1'  // Change to 'S1', 'P1V2', etc.
     capacity: 1
   }
   ```

2. **Document Intelligence SKU**:
   ```bicep
   param documentIntelligenceSku string = 'S0'  // Change to 'F0' for free tier
   ```

3. **Location**:
   ```powershell
   azd env set AZURE_LOCATION westus2
   ```

## Useful Commands

```powershell
# View all deployed resources
azd show

# View environment values
azd env get-values

# View deployment logs
azd logs

# Update infrastructure (after changing Bicep files)
azd provision

# Redeploy application code
azd deploy

# Full deployment (provision + deploy)
azd up

# Clean up all resources
azd down
```

## Troubleshooting

### 1. Managed Identity Not Working

Check role assignments:
```powershell
az role assignment list --assignee <managed-identity-principal-id> --scope <document-intelligence-resource-id>
```

### 2. Application Not Starting

Check application logs:
```powershell
azd logs
# Or
az webapp log tail --name <app-service-name> --resource-group <resource-group-name>
```

### 3. Document Intelligence Errors

Verify endpoint configuration:
```powershell
az webapp config appsettings list --name <app-service-name> --resource-group <resource-group-name>
```

## Security Best Practices

✅ **Managed Identity**: No API keys stored in configuration
✅ **HTTPS Only**: All traffic is encrypted
✅ **Minimal Permissions**: Managed Identity has only required permissions
✅ **Network Security**: Can be enhanced with VNet integration
✅ **Monitoring**: Application Insights enabled by default

## Cost Estimation

Approximate monthly costs (East US region):

| Resource | SKU | Estimated Cost |
|----------|-----|----------------|
| App Service (B1) | Basic | ~$13 |
| Document Intelligence (S0) | Standard | ~$1.50/1K pages |
| Application Insights | Pay-as-you-go | ~$5 (low volume) |
| **Total** | | **~$18-20/month** |

*Costs vary by region and usage*

## Next Steps

1. **Configure Custom Domain**: Add your own domain name
2. **Enable Authentication**: Add Azure AD authentication
3. **Scale Up**: Upgrade to higher SKUs for production
4. **Add VNet**: Integrate with Virtual Network for enhanced security
5. **CI/CD**: Set up GitHub Actions with `azd pipeline config`

## Support

For issues or questions:
- Azure Developer CLI: https://aka.ms/azd
- Azure Documentation: https://docs.microsoft.com/azure
