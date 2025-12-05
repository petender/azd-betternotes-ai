# BetterNotes - AI Document Analysis

An ASP.NET Core web application that uses Azure Document Intelligence (Form Recognizer) to analyze PDF and DOCX files and generate formatted Word documents with the extracted content. Deployed on Windows App Service with .NET 9 using passwordless authentication.

## Features

- 📄 Upload PDF or DOCX files
- 🤖 AI-powered document analysis using Azure Document Intelligence
- 📥 Download results as formatted Word documents
- 🎨 Modern, responsive UI with progress indicators
- 🔐 Passwordless authentication with Managed Identity
- ☁️ Azure Blob Storage for file management (no local file system dependencies)
- 🪟 Windows App Service deployment with .NET 9

## Architecture

This application uses a fully passwordless architecture with Azure Managed Identity:

- **Windows App Service (.NET 9)** - Hosts the web application
- **Azure Document Intelligence** - Analyzes document content
- **Azure Blob Storage** - Stores uploaded and processed files
- **Managed Identity** - Provides secure, keyless authentication to all Azure services

## Authentication Methods

The application supports two authentication methods:

### 1. API Key Authentication (Local Development)

- Used when running locally with `ASPNETCORE_ENVIRONMENT=Development`
- API key is stored in `appsettings.Development.json`
- **Note:** `appsettings.Development.json` should not be committed to source control

### 2. Managed Identity Authentication (Azure Deployment)

- Used when deployed to Azure App Service
- No API keys or connection strings required
- Uses system-assigned Managed Identity for all Azure services
- More secure and follows Azure best practices

## Configuration

### Local Development Setup

1. Create `appsettings.Development.json` with your Azure AI credentials:

```json
{
  "AzureAI": {
    "Endpoint": "https://<your-resource>.cognitiveservices.azure.com/",
    "Key": "<your-api-key>"
  }
}
```

2. Run the application:

```bash
dotnet run
```

3. Navigate to `http://localhost:5000`

### Azure Deployment with Managed Identity

When deploying to Azure App Service, the application will automatically use Managed Identity for both Document Intelligence and Blob Storage:

#### Automated Deployment with Azure Developer CLI (azd)

The easiest way to deploy is using `azd`:

```bash
# Initialize the environment
azd init

# Provision and deploy
azd up
```

This will automatically:

- Create a Windows App Service with .NET 9
- Create Azure Document Intelligence resource
- Create Azure Blob Storage account with containers
- Enable system-assigned Managed Identity
- Assign required RBAC roles:
  - **Cognitive Services User** on Document Intelligence
  - **Storage Blob Data Contributor** on Storage Account
- Configure all app settings

#### Manual Deployment Steps

If deploying manually:

1. **Create Azure Resources**:

   ```bash
   # Create resource group
   az group create --name <resource-group> --location eastus2
   
   # Create Windows App Service Plan
   az appservice plan create \
     --name <plan-name> \
     --resource-group <resource-group> \
     --sku B1 \
     --is-linux false
   
   # Create Web App with .NET 9
   az webapp create \
     --name <app-name> \
     --resource-group <resource-group> \
     --plan <plan-name> \
     --runtime "DOTNET|9.0"
   
   # Create Document Intelligence
   az cognitiveservices account create \
     --name <ai-name> \
     --resource-group <resource-group> \
     --kind FormRecognizer \
     --sku S0 \
     --location eastus2
   
   # Create Storage Account
   az storage account create \
     --name <storage-name> \
     --resource-group <resource-group> \
     --location eastus2 \
     --sku Standard_LRS \
     --allow-blob-public-access false
   
   # Create containers
   az storage container create --name uploads --account-name <storage-name> --auth-mode login
   az storage container create --name downloads --account-name <storage-name> --auth-mode login
   ```

2. **Enable Managed Identity** on your Azure App Service:

   ```bash
   az webapp identity assign --name <app-name> --resource-group <resource-group>
   ```

3. **Grant the Managed Identity access** to Azure services:

   ```bash
   # Get the principal ID of the managed identity
   principalId=$(az webapp identity show --name <app-name> --resource-group <resource-group> --query principalId -o tsv)
   
   # Assign "Cognitive Services User" role for Document Intelligence
   az role assignment create \
     --assignee $principalId \
     --role "Cognitive Services User" \
     --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<ai-name>
   
   # Assign "Storage Blob Data Contributor" role for Blob Storage
   az role assignment create \
     --assignee $principalId \
     --role "Storage Blob Data Contributor" \
     --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Storage/storageAccounts/<storage-name>
   ```

4. **Configure App Settings** in Azure App Service:

   ```bash
   az webapp config appsettings set \
     --name <app-name> \
     --resource-group <resource-group> \
     --settings \
       AzureAI:Endpoint="https://<ai-name>.cognitiveservices.azure.com/" \
       AzureStorage:AccountName="<storage-name>" \
       AzureStorage:UploadContainer="uploads" \
       AzureStorage:DownloadContainer="downloads"
   ```
   
   **Important**: Do NOT set API keys or connection strings. The application automatically uses Managed Identity when credentials are not provided.

5. **Deploy your application** to Azure App Service:

   ```bash
   az webapp deployment source config-zip \
     --name <app-name> \
     --resource-group <resource-group> \
     --src <path-to-zip>
   ```

6. **Wait for role propagation**: After creating role assignments, wait 5-10 minutes for Azure RBAC to propagate before testing.

## Required Azure Resources

- **Azure App Service** (Windows, .NET 9)
- **Azure Document Intelligence** (Cognitive Services - Form Recognizer)
- **Azure Blob Storage** (with `uploads` and `downloads` containers)
- **System-assigned Managed Identity** enabled on the App Service
- **Role Assignments**: 
  - The App Service's Managed Identity needs **"Cognitive Services User"** role on the Document Intelligence resource
  - The App Service's Managed Identity needs **"Storage Blob Data Contributor"** role on the Storage Account

## Project Structure

```
BetterNotes/
├── infra/                      # Azure infrastructure (Bicep templates)
│   ├── main.bicep              # Main orchestration
│   ├── main.parameters.json    # Environment parameters
│   └── core/                   # Modular resource templates
│       ├── host/               # App Service resources
│       ├── ai/                 # Document Intelligence
│       ├── storage/            # Blob Storage
│       └── security/           # Role assignments
├── Pages/
│   ├── Upload.cshtml           # Main UI page
│   ├── Upload.cshtml.cs        # Upload and analysis logic
│   ├── Download.cshtml         # Download page
│   └── Download.cshtml.cs      # File serving logic
├── Services/
│   ├── AzureAIService.cs       # Azure Document Intelligence integration
│   ├── BlobStorageService.cs   # Azure Blob Storage operations
│   └── FileProcessingService.cs # Word document generation
├── appsettings.json            # Production settings (no keys)
├── appsettings.Development.json # Development settings (with API key)
├── azure.yaml                  # Azure Developer CLI config
└── Program.cs                  # Application startup

```

## Security Best Practices

✅ **Use Managed Identity in Production** - No secrets in configuration or code  
✅ **Passwordless authentication** - No API keys or connection strings in production  
✅ **API keys only for local development** - Store in `appsettings.Development.json` (not in source control)  
✅ **Role-based access control** - Grant minimum required permissions (Cognitive Services User + Storage Blob Data Contributor)  
✅ **Azure Blob Storage** - No local file system dependencies, all files in cloud storage  
✅ **Storage account key authentication disabled** - Only Azure AD authentication allowed  
✅ **HTTPS enforcement** - Application uses HTTPS redirection  
✅ **Antiforgery tokens** - Forms are protected against CSRF attacks

## Development Workflow

1. Clone the repository
2. Create `appsettings.Development.json` with your Azure AI credentials
3. Run `dotnet restore`
4. Run `dotnet run`
5. Open browser to `http://localhost:5000`

## Deployment Workflow

### Using Azure Developer CLI (Recommended)

```bash
# One-time setup
azd init

# Deploy everything (infrastructure + app)
azd up

# Or deploy just the app after infrastructure is created
azd deploy
```

### Manual Deployment

1. Provision Azure resources (App Service, Document Intelligence, Storage)
2. Enable System-assigned Managed Identity on App Service
3. Grant the Managed Identity required roles on Document Intelligence and Storage
4. Configure app settings (`AzureAI:Endpoint`, `AzureStorage:AccountName`)
5. Wait 5-10 minutes for role assignments to propagate
6. Deploy application code
7. Application will automatically use Managed Identity for authentication

## Common Issues and Solutions

### "Failed to authenticate with Managed Identity"

**Cause**: Role assignments haven't propagated yet, or incorrect header placement in HTTP requests.

**Solution**: 
- Wait 5-10 minutes after creating role assignments
- Verify role assignments exist: `az role assignment list --assignee <principal-id>`
- Ensure Authorization headers are added to HttpRequestMessage, not HttpContent

### "Access denied (403)" on blob operations

**Cause**: Missing Storage Blob Data Contributor role, or storage account key authentication is enforced.

**Solution**:
- Assign "Storage Blob Data Contributor" role to the managed identity
- Ensure storage account allows Azure AD authentication
- Check that role is scoped to the storage account level

### "Resource not found" errors

**Cause**: App settings have incorrect resource names or endpoints.

**Solution**:
- Verify `AzureAI:Endpoint` matches your Document Intelligence resource
- Verify `AzureStorage:AccountName` matches your storage account name
- Check for typos in app settings (use colons, not double underscores in Bicep)

## Technologies Used

- ASP.NET Core 8.0 (deployed on .NET 9 runtime)
- Windows App Service
- Azure Document Intelligence (Form Recognizer)
- Azure Blob Storage
- Azure Identity SDK (DefaultAzureCredential for Managed Identity)
- Azure.Storage.Blobs SDK (passwordless authentication)
- DocumentFormat.OpenXml (for Word document generation)
- Razor Pages
- Azure Developer CLI (azd) for deployment
- Bicep for Infrastructure as Code

## License

This project is for demonstration purposes.
