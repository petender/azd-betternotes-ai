# BetterNotes - AI Document Analysis - Demo Scenario

This demo scenario deploys an ASP.NET Core web application that uses Azure Document Intelligence (Form Recognizer) to analyze PDF, DOCX or Image files and generate formatted Word documents with the extracted content. Deployed on Windows App Service with .NET 9 using passwordless (Managed Identity) authentication.

This scenario is part of the broader [Trainer-Demo-Deploy](https://aka.ms/trainer-demo-deploy) Open Source Project. Contributions are very welcome and encouraged! 

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

#### Automated Deployment with Azure Developer CLI (azd) (Recommended)

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

## Project Structure

```bash
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

## License

This project is for demonstration purposes.
