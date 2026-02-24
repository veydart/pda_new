---
description: How to start the PDA Analytics application (all services)
---

# Запуск PDA Analytics

## Порты
- **Backend API**: `http://localhost:5119`
- **Frontend (Vite)**: `http://localhost:5175`
- **Syncer**: отдельный worker-process (нет HTTP)

## Шаги запуска

### 1. Backend API
// turbo
```bash
cd H:/NR/pda_new
dotnet run --project src/PdaAnalytics.Api --urls "http://localhost:5119"
```
Ждём: `Now listening on: http://localhost:5119`

### 2. Frontend (React + Vite)
// turbo
```bash
cd H:/NR/pda_new/client
npm run dev -- --port 5175
```
Ждём: `Local: http://localhost:5175/`

### 3. Data Syncer (ETL Worker) — опционально
// turbo
```bash
cd H:/NR/pda_new
dotnet run --project src/PdaAnalytics.Syncer
```
Ждём: `DataSyncWorker запущен`

## Учётные данные
- **SuperAdmin**: `admin` / `admin`

## Технологии
- Backend: .NET 10, EF Core, PostgreSQL, JWT Auth
- Frontend: React 18 + TypeScript, Vite, Axios
- Database: PostgreSQL (pda_analytics), MariaDB (game instances)
