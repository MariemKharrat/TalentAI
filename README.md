# CareerApp – Recruitment AI Platform

An AI-powered recruitment application that augments the hiring process using Azure AI services. Built with **.NET 10** (backend) and **React TypeScript** (frontend), integrated with **Azure AI Foundry** for intelligent automation.

---

## 🎯 Use Cases

| # | Feature | Description |
|---|---------|-------------|
| 1 | **CV Parsing** | Automatically extract structured data from uploaded CVs (PDF/DOCX) and create candidate profiles |
| 2 | **CV-to-Job Matching** | Score and rank candidates against job descriptions using AI (High/Medium/Low match levels) |
| 3 | **AI Job Description Generation** | Generate policy-compliant job descriptions grounded in organizational rules via RAG |
| 4 | **Candidate Recommendations** | Suggest matching jobs to candidates based on their skills and experience |

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        React Frontend                            │
│   Dashboard │ Candidates │ Jobs │ Matching │ CV Upload           │
└──────────────────────────────┬──────────────────────────────────┘
                               │ REST API (HTTPS)
┌──────────────────────────────┴──────────────────────────────────┐
│                     .NET 10 Web API                               │
│  CandidatesController │ JobsController │ MatchingController       │
├─────────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                           │
│  CvParsingService │ JobMatchingService │ JobDescriptionGenerator  │
├─────────────────────────────────────────────────────────────────┤
│                      Azure AI Services                           │
│  Document Intelligence │ Azure OpenAI (Foundry) │ Knowledge Base  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📁 Project Structure

```
CareerApp/
├── src/
│   ├── CareerApp.API/              # ASP.NET Core 10 Web API
│   │   ├── Controllers/            # REST endpoints
│   │   ├── Program.cs              # Service registration & middleware
│   │   └── appsettings.json        # Configuration
│   ├── CareerApp.Core/             # Domain layer (models, interfaces)
│   │   ├── Models/                 # Candidate, Job, MatchResult, etc.
│   │   ├── Interfaces/             # Service & repository contracts
│   │   └── DTOs/                   # Request/response objects
│   └── CareerApp.Infrastructure/   # Implementation layer
│       ├── Configuration/          # AzureAIOptions
│       ├── Data/                   # EF Core DbContext
│       ├── Repositories/           # Data access
│       └── Services/               # Azure AI service integrations
├── client/                         # React TypeScript frontend
│   ├── src/
│   │   ├── components/             # Navbar, FileUpload, MatchScoreBadge, SkillTag
│   │   ├── pages/                  # Dashboard, Candidates, Jobs, etc.
│   │   ├── services/               # Axios API client
│   │   └── types/                  # TypeScript interfaces
├── tests/
│   └── CareerApp.Tests/            # xUnit test project
├── CareerApp.slnx                  # .NET solution file
└── .gitignore
```

---

## ☁️ Azure Resources Required

You need to create the following resources in your Azure subscription:

### 1. Azure AI Foundry (Hub + Project)
- **What**: Central hub for managing AI models and deployments
- **Create**: [Azure AI Foundry Portal](https://ai.azure.com) → Create a new project
- **Deploy models**:
  - `gpt-4o` (or `gpt-4o-mini`) – for job matching, description generation, and candidate recommendations
- **Get**: Endpoint URL + API Key from the project settings

### 2. Azure AI Document Intelligence
- **What**: Extracts structured data from CVs (PDF, DOCX, images)
- **Create**: Azure Portal → Create resource → "Document Intelligence"
- **Pricing tier**: S0 (Standard) for production, F0 (Free) for testing
- **Get**: Endpoint URL + API Key from Keys and Endpoint blade

### 3. Azure SQL Database
- **What**: Stores candidates, jobs, and match results
- **Create**: Azure Portal → Create resource → "SQL Database"
- **Configuration**: Basic/Standard tier for dev, scale as needed
- **Get**: Connection string from the database overview

### 4. Azure App Service (for deployment)
- **What**: Hosts the .NET API backend
- **Create**: Azure Portal → Create resource → "App Service"
- **Runtime**: .NET 10
- **Plan**: B1 or higher for production

### 5. Azure Static Web Apps (for frontend)
- **What**: Hosts the React frontend
- **Create**: Azure Portal → Create resource → "Static Web App"
- **Connect**: To your GitHub repo's `client/` folder

### 6. (Optional) Azure AI Search + Knowledge Base
- **What**: For RAG-based job description generation grounded in policy documents
- **Create**: Azure Portal → "AI Search" service
- **Upload**: Your government/organizational policy documents
- **Connect**: To your AI Foundry project as a knowledge source

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/)
- [SQL Server LocalDB](https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (or Azure SQL)
- Azure subscription with the resources above created

### 1. Clone the repository
```bash
git clone https://github.com/MariemKharrat/CareerApp.git
cd CareerApp
```

### 2. Configure Azure AI credentials

Edit `src/CareerApp.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CareerApp;Trusted_Connection=True;"
  },
  "AzureAI": {
    "DocumentIntelligenceEndpoint": "https://<your-doc-intel>.cognitiveservices.azure.com/",
    "DocumentIntelligenceKey": "<your-key>",
    "OpenAIEndpoint": "https://<your-openai>.openai.azure.com/",
    "OpenAIKey": "<your-key>",
    "OpenAIDeploymentName": "gpt-4o"
  }
}
```

> ⚠️ **Never commit secrets!** Use User Secrets or Azure Key Vault for production:
> ```bash
> cd src/CareerApp.API
> dotnet user-secrets set "AzureAI:OpenAIKey" "<your-key>"
> dotnet user-secrets set "AzureAI:DocumentIntelligenceKey" "<your-key>"
> ```

### 3. Set up the database
```bash
cd src/CareerApp.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Run the backend
```bash
cd src/CareerApp.API
dotnet run
```
The API will start at `https://localhost:5001` (Swagger UI available at `/swagger`).

### 5. Run the frontend
```bash
cd client
npm install
npm start
```
The React app will open at `http://localhost:3000`.

---

## 📡 API Endpoints

### Candidates
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/candidates/upload-cv` | Upload CV file → parse & create profile |
| GET | `/api/candidates` | List all candidates |
| GET | `/api/candidates/{id}` | Get candidate details |
| GET | `/api/candidates/{id}/matches` | Get job matches for candidate |
| DELETE | `/api/candidates/{id}` | Delete candidate |

### Jobs
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/jobs` | List active jobs |
| GET | `/api/jobs/{id}` | Get job details |
| POST | `/api/jobs` | Create job |
| PUT | `/api/jobs/{id}` | Update job |
| DELETE | `/api/jobs/{id}` | Delete job |
| POST | `/api/jobs/generate-description` | AI-generate job description |
| GET | `/api/jobs/{id}/candidates` | Get matched candidates |

### Matching
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/matching/candidate/{id}/job/{jobId}` | Match one candidate to one job |
| POST | `/api/matching/candidate/{id}/all-jobs` | Match candidate to all jobs |
| POST | `/api/matching/job/{id}/all-candidates` | Match job to all candidates |

---

## 🔒 Security Considerations

- **Government context**: AI must respect document sensitivity and enforce access control
- **Policy grounding**: Job descriptions are generated through RAG to ensure compliance with organizational policies
- **Identity**: Plan to integrate Azure AD (Entra ID) for authentication with MFA
- **Data privacy**: CV data is processed in your Azure tenant – no external third-party access

---

## 🛣️ Roadmap

- [ ] Azure AI Foundry model deployment and endpoint configuration
- [ ] Policy document upload to knowledge base (RAG)
- [ ] Azure AD authentication integration
- [ ] CI/CD pipeline with GitHub Actions
- [ ] Containerization with Docker
- [ ] Azure App Service deployment
- [ ] Recruiter AI chat assistant (embedded agent)
- [ ] Batch CV processing

---

## 🧪 Running Tests

```bash
dotnet test tests/CareerApp.Tests/CareerApp.Tests.csproj
```

---

## 📄 License

Private project – DSTI / Microsoft engagement.
