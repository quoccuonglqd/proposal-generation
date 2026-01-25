# Cherry Project - Proposal Generation Tool

A centralized system for generating PowerPoint and PDF proposals with region-specific pricing and hierarchical service selection.

## Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Required)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (For local development)
- [Node.js 20+](https://nodejs.org/) (For local development)

---

## 🚀 Quick Start with Docker (Production)

### 1. Configure Credentials
Before first run, edit `backend/Cherry.Api/appsettings.json` to set your initial admin credentials in the `SeedData` section (or use environment variables in docker-compose.yml).

### 2. Start Everything
```bash
docker-compose up -d
```

This single command will:
- ✅ Start PostgreSQL, Redis, and MinIO
- ✅ Build and start the backend API (with Hangfire workers)
- ✅ Build and start the frontend Next.js app
- ✅ Automatically initialize the database and seed data

### 3. Access the Application
- **Web App**: [http://localhost:3000](http://localhost:3000)
- **API Swagger**: [http://localhost:5008/swagger](http://localhost:5008/swagger)
- **Hangfire Dashboard**: [http://localhost:5008/hangfire](http://localhost:5008/hangfire)

### Default Credentials
- **Admin**: `admin@cherry.com` / `Admin123!`
- **Creator**: `creator@cherry.com` / `Creator123!`
- **Viewer**: `viewer@cherry.com` / `Viewer123!`

---

## 🛠️ Local Development Mode

If you want to develop locally (without Docker for backend/frontend):

### 1. Start Infrastructure Only
```bash
docker-compose up -d db redis minio
```

### 2. Run Backend Locally
```bash
cd backend
dotnet run --project Cherry.Api
```

### 3. Run Frontend Locally
```bash
cd frontend
npm install --legacy-peer-deps
npm run dev
```

---

## 🏗️ Architecture

**Docker Services:**
- **db** - PostgreSQL 16 (database)
- **redis** - Redis 7 (cache & session)
- **minio** - MinIO (S3-compatible object storage)
- **backend** - .NET 8 API + Hangfire Workers (proposal generation)
- **frontend** - Next.js 14 (web interface)

**Key Features:**
- ✅ Role-Based Access Control (Admin, Proposal Creator, Viewer)
- ✅ Background job processing for PPTX/PDF generation
- ✅ Multi-region pricing with configurable exchange rates
- ✅ Template versioning and management

---

## 📋 How to Use

1. **Login**: Visit [http://localhost:3000](http://localhost:3000) and login with admin credentials
2. **Create Proposal**: Click "New Proposal" → Select region → Choose services → Configure sections
3. **Generate Documents**: Open a proposal → Click "Generate" → Download PPTX/PDF artifacts

---

## 🔐 Security & Credentials

### Initial Setup
All initial accounts are defined in `docker-compose.yml` under the `backend` service environment variables:
- `SeedData__Admin__Email` / `SeedData__Admin__Password`
- `SeedData__Creator__Email` / `SeedData__Creator__Password`
- `SeedData__Viewer__Email` / `SeedData__Viewer__Password`

**For production**, change these before deploying!

### Managing Users
Once logged in as Admin:
1. Navigate to **Admin Console** → **Users**
2. View all registered users and change their roles
3. Promote/demote users between Admin, Proposal Creator, and Viewer

---

## 🗄️ Database Management

### Reset Everything
```bash
docker-compose down -v  # Remove containers AND volumes
docker-compose up -d     # Fresh start with re-seeding
```

### Backup Database
```bash
docker exec cherry-db pg_dump -U admin cherry > backup.sql
```

### Restore Database
```bash
docker exec -i cherry-db psql -U admin cherry < backup.sql
```

---

## 🐛 Troubleshooting

**Backend not starting?**
- Check logs: `docker logs cherry-backend`
- Verify DB is ready: `docker ps | grep cherry-db`

**Frontend can't connect to API?**
- Ensure backend is running: `docker ps | grep cherry-backend`
- Check NEXT_PUBLIC_API_URL in docker-compose.yml

**Need to rebuild after code changes?**
```bash
docker-compose up -d --build
```

---

## 📁 Project Structure
- `/backend` - .NET 8 Web API, Workers, Core logic, Data Access
- `/frontend` - Next.js 14 with Material UI and TypeScript
- `/docker-compose.yml` - Full stack orchestration

---

## 🔄 CI/CD & Deployment

The Dockerfiles use multi-stage builds for optimal production images:
- **Backend**: ~200MB (runtime only, no SDK)
- **Frontend**: ~150MB (standalone Next.js output)

For production deployment, update:
1. Database connection strings
2. JWT secrets
3. Initial admin credentials
4. API URL for frontend
