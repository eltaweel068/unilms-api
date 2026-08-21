# UniLMS — AI-Powered University Learning Management System

Backend REST API for a university-grade Learning Management System built with ASP.NET Core 8.
Administrators manage courses organized by academic level and department, upload lecture materials,
and the system automatically indexes each file into an AI engine. Students interact with a RAG-based
AI tutor that answers questions strictly grounded in the uploaded course content, with responses
streamed in real time via SignalR.

Full bilingual and localization support is built-in for English and Arabic across course titles,
curriculum descriptions, lecture summaries, and AI tutoring interactions.

---

## Architecture

```
Frontend (React)
      |
      | HTTP / WebSocket
      v
ASP.NET Core 8 API  ──── REST/SSE ────>  AI Engine (Hugging Face Spaces)
  (MonsterASP.NET)                        grad90-unilms-ai-engine
      |
      | EF Core
      v
PostgreSQL (Supabase)
      |
      v
Supabase Storage (bucket: lectures)
```

---

## Technology Stack

| Layer           | Technology                                           |
|-----------------|------------------------------------------------------|
| Runtime         | .NET 8 / ASP.NET Core 8 Web API                      |
| Hosting         | MonsterASP.NET                                       |
| Database        | PostgreSQL via Supabase                              |
| ORM             | Entity Framework Core 8                              |
| Authentication  | JWT Bearer (7-day expiry) + BCrypt                   |
| Real-Time       | SignalR (WebSockets)                                 |
| AI Streaming    | Server-Sent Events (SSE)                             |
| File Storage    | Supabase Storage (bucket: `lectures`)                |
| Email           | MailKit (Gmail SMTP)                                 |
| AI Engine       | Hugging Face Spaces                                  |
| API Docs        | Swagger / OpenAPI 3.0                                |

---

## Key Features

- Localization & Bilingual Support — Full Arabic (AR) and English (EN) support across course titles, descriptions, lecture summaries, and AI conversations
- JWT role-based access — Admin and Student roles
- Academic structure — 4 levels, 3 departments (CS / IT / IS) for levels 3 and 4
- Lecture upload pipeline — file → SHA-256 dedup → Supabase Storage → AI ingestion → AI summary
- One material per lecture per course enforced at both application and database level
- Real-time AI chat — token-by-token streaming via SSE and SignalR broadcast
- OTP-based password recovery via Gmail SMTP
- Student email notifications on every new lecture upload
- Auto-creation of the Supabase Storage bucket on startup

---

## Project Structure

```
UniLMS.API/
├── Controllers/
│   ├── AuthController.cs           # Register, Login, OTP, Password Reset, Students list
│   ├── LevelsController.cs         # List levels, get subjects by level and department
│   ├── SubjectsController.cs       # Lecture material upload, list, delete
│   └── ChatController.cs           # Send message (streaming), history, clear
├── Models/
│   ├── Entities/                   # User, Course, LevelInfo, CourseMaterial, ChatMessage, OtpToken
│   └── DTOs/                       # Request and response shapes per feature
├── Data/
│   ├── AppDbContext.cs             # EF Core DbContext with Fluent API configuration
│   └── DbSeeder.cs                 # Seeds default admin and 4 academic levels on startup
├── Services/
│   ├── Interfaces/                 # IAuthService, ICourseService, IChatService, IEmailService, IFileStorageService
│   └── Implementations/            # AuthService, CourseService, ChatService, EmailService, SupabaseStorageService
├── Middleware/
│   └── ExceptionMiddleware.cs      # Global exception handler — returns structured JSON
├── Hubs/
│   └── ChatHub.cs                  # SignalR ChatHub and NotificationHub
├── Helpers/
│   └── ApiResponse.cs              # Generic ApiResponse<T> and PagedResponse<T>
├── Migrations/                     # EF Core migration history
├── Scripts/
│   └── supabase-setup.sql          # Optional one-time SQL setup for Supabase
├── Program.cs                      # DI registration, middleware pipeline, startup checks
└── appsettings.json                # Configuration template (no secrets committed)
```

---

## API Overview

| Method   | Endpoint                                           | Auth   | Role  |
|----------|----------------------------------------------------|--------|-------|
| POST     | `/api/Auth/register`                               | Public | —     |
| POST     | `/api/Auth/login`                                  | Public | —     |
| POST     | `/api/Auth/forgot-password`                        | Public | —     |
| POST     | `/api/Auth/reset-password`                         | Public | —     |
| POST     | `/api/Auth/change-password`                        | Bearer | Any   |
| GET      | `/api/Auth/students`                               | Bearer | Admin |
| GET      | `/api/Levels`                                      | Bearer | Any   |
| GET      | `/api/Levels/{levelNumber}/subjects`               | Bearer | Any   |
| GET      | `/api/subjects/{subjectId}/materials`              | Bearer | Any   |
| POST     | `/api/subjects/{subjectId}/materials`              | Bearer | Admin |
| DELETE   | `/api/subjects/{subjectId}/materials/{materialId}` | Bearer | Admin |
| POST     | `/api/Chat/send`                                   | Bearer | Any   |
| GET      | `/api/Chat/history/{courseId}`                     | Bearer | Any   |
| DELETE   | `/api/Chat/history/{courseId}`                     | Bearer | Any   |
| WS       | `/hubs/chat`                                       | Bearer | Any   |
| WS       | `/hubs/notifications`                              | Bearer | Any   |

See [DOCUMENTATION.md](./DOCUMENTATION.md) for the full endpoint reference including request bodies, response shapes, error codes, and SignalR event definitions.

---

## Role Permissions

| Action                     | Student | Admin |
|----------------------------|:-------:|:-----:|
| Register / Login           | Yes     | Yes   |
| View levels and subjects   | Yes     | Yes   |
| View lecture materials     | Yes     | Yes   |
| Chat with AI (streaming)   | Yes     | Yes   |
| Upload lecture material    | No      | Yes   |
| Delete material            | No      | Yes   |
| List all students          | No      | Yes   |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Supabase](https://supabase.com) account (free tier)
- Gmail account with an [App Password](https://support.google.com/accounts/answer/185833) configured

### Configure

Copy the template and fill in your credentials:

```bash
cp appsettings.json appsettings.Development.json
```

Replace all `YOUR_*` placeholders in `appsettings.Development.json` with your actual values.

### Run

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Swagger UI: `http://localhost:5000/swagger`

The Supabase Storage bucket (`lectures`) is created automatically on first startup.

---

## Environment Variables

| Key                                   | Required | Description                                     |
|---------------------------------------|----------|-------------------------------------------------|
| `ConnectionStrings:DefaultConnection` | Yes      | Supabase PostgreSQL connection string           |
| `Supabase:Url`                        | Yes      | Supabase project URL                            |
| `Supabase:AnonKey`                    | Yes      | Supabase anonymous key                          |
| `Supabase:ServiceRoleKey`             | Yes      | Supabase service role key (used for storage)    |
| `Jwt:Secret`                          | Yes      | JWT signing secret (minimum 32 characters)      |
| `Jwt:Issuer`                          | Yes      | JWT issuer — default: `UniLMS.API`              |
| `Jwt:Audience`                        | Yes      | JWT audience — default: `UniLMS.Client`         |
| `Email:SmtpHost`                      | Yes      | SMTP host — e.g. `smtp.gmail.com`               |
| `Email:SmtpPort`                      | Yes      | SMTP port — e.g. `587`                          |
| `Email:SmtpUser`                      | Yes      | Gmail address                                   |
| `Email:SmtpPass`                      | Yes      | Gmail App Password                              |
| `Email:SenderName`                    | No       | Display name for outgoing emails                |
| `Email:SenderEmail`                   | No       | From address for outgoing emails                |
| `AiService:BaseUrl`                   | Yes      | AI engine URL (Hugging Face Spaces)             |
| `AiIngestService:BaseUrl`             | Yes      | AI engine URL for file ingestion                |
| `Cors:AllowedOrigins`                 | Yes      | JSON array of allowed frontend origins          |
| `Seed:AdminEmail`                     | No       | Default admin email (default: admin@unilms.com) |
| `Seed:AdminPassword`                  | No       | Default admin password (default: Admin@123456)  |

