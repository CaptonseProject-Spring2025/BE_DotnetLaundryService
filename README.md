# 🧺 Laundry Service – .NET Backend System
[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-ready-blue?logo=docker)]()
[![GitHub Actions](https://img.shields.io/badge/CI/CD-GitHub_Actions-success)]()
[![License](https://img.shields.io/badge/license-MIT-green)]()

## 📖 Overview
Laundry Service is a backend system built with ASP.NET Core, following N-Tier architecture and Repository + Unit of Work patterns.  
It provides secure and efficient management for laundry orders, payments, and staff workflows.

## ✨ Features
- 🧾 RESTful APIs for full order lifecycle (create, update, payment, quality check)
- 🔐 Role-based JWT Authentication
- 💳 PayOS QR & bank-transfer payment integration
- 🗺️ Mapbox for address and geolocation flows
- ☁️ Backblaze B2 storage with signed URLs
- ⚙️ Dockerized deployment with Nginx reverse proxy
- 🚀 GitHub Actions CI/CD pipeline

## 🧱 System Architecture
LaundryService/
├── LaundryService.Api/              # ASP.NET Core Web API layer
├── LaundryService.Domain/           # Entities, enums, domain logic
├── LaundryService.Dto/              # Data Transfer Objects
├── LaundryService.Infrastructure/   # Repositories, EF Core, database context
├── LaundryService.Service/          # Business logic, services
├── docker-compose.yml               # Container orchestration
└── LaundryService.sln               # Solution file

## 🛠 Tech Stack
- **Backend:** ASP.NET Core 8.0, C#, Entity Framework Core  
- **Database:** SQL Server  
- **Storage:** Backblaze B2 (S3-compatible)  
- **Payment:** PayOS  
- **Map Service:** Mapbox  
- **DevOps:** Docker, Nginx, GitHub Actions CI/CD  

## 🚀 Deployment
The backend is containerized using Docker and deployed to a VPS behind Nginx reverse proxy.  
GitHub Actions automates build, test, and deployment with zero-downtime rolling updates.

## 📚 API Documentation
Swagger UI available at: