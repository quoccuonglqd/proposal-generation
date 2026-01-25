---
description: How to run the Proposal Generation Tool
---
// turbo-all

1. Start Infrastructure
```bash
docker-compose up -d
```

2. Run Backend API
```bash
cd backend && dotnet run --project Cherry.Api
```

3. Run Frontend
```bash
cd frontend && npm run dev
```
