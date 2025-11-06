# Laundry Service Application - Ecolaundry
Capstone project - Building Laundry Service Application for Green Shine Trading Service Company Limited
[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-ready-blue?logo=docker)]()
[![GitHub Actions](https://img.shields.io/badge/CI/CD-GitHub_Actions-success)]()

## Overview

**Project Title:** Building Laundry Service Application for Green Shine Trading Service Company Limited  

Green Shine currently operates a walk-in laundry model that limits customer convenience, reduces service reach, and creates manual bottlenecks across ordering, scheduling, and communications. Customers must visit physical shops, cannot easily schedule off-hour services, and lack real-time visibility over order status. Internally, staff coordination and delivery tracking are fragmented, leading to delays and inconsistent experiences.

This project proposes a **mobile-first, end-to-end digital platform** that connects customers, drivers, laundry staff, customer service, and administrators. The solution enables anytime/anywhere ordering, flexible pickup & delivery scheduling, real-time tracking, integrated payments, and transparent two-way communication.  

By digitizing the full service lifecycle, Green Shine aims to improve operational efficiency, raise customer satisfaction and retention, and build a scalable foundation for growth.

### **Target Platforms & Components**
- **Mobile App** (primary touchpoint) for Customers, Drivers, and Admins.  
- **Web App** for Admin, Laundry Staff, and Customer Service operations.  
- **Backend Web API** providing secure, role-based services, data management, and integrations (authentication, payments, notifications, maps).

## Tech Stack
- **Backend:** ASP.NET Core 8.0, C#, Entity Framework Core  
- **Database:** PostgreSQL
- **Storage:** Backblaze B2 (S3-compatible)  
- **Payment:** PayOS
- **Map Service:** Mapbox
- **DevOps:** Docker, Nginx, GitHub Actions CI/CD  


## ⚙️ Functional Requirements

### **1. Administrator**
- **Account Management:** Create/edit/delete/ban accounts; role/permission control.
- **Order Management:** View orders, view assign orders history, assign order (assign order to staff, assign order to driver)
- **Employee Management:** view employees performance , view employees work schedule.
- **Service & Pricing:** Import via Excel; create/update/delete services; promotions.
- **Complaint Management:** View complaint, track complaint status, handle customer complaints.

### **3. Customer**
- **Account Management:** Register, login, forgot password, change password and manage profile (personal info, address,...).
- **Manage Personal Orders:** Place laundry orders, cancel order and view order history.
- **Payment:** Make payments through multiple methods.
- **Track Orders:** Receive real-time updates and notifications about order status.
- **Track Driver Location:** View the driver’s location on the map during delivery.
- **Service Rating:** Rate the service and provide feedback after the delivery.
- **Chat Support:** Communicate with deliver driver for any issues or inquiries.

### **4. Driver**
- **Management Delivery Order:** Log into the driver app, view new orders for pickup and delivery.
- **Information Order Delivery:** View order details
- **Status Updates:** Update order statuses .
- **Location Sharing:** Share location in real-time for customer tracking.
- **Customer Communication:** Chat with customers for clarification or issues.
- **Work History:** Access past orders and delivery records.


### **5. Laundry Staff**
- **Order Management:** Log into the system, view laundry orders to be processed.
- **Order Status Updates:** Update the status of laundry orders.
- **Handle Premium Services:** Manage high-end laundry services (special garments or premium packages).
- **Performance Monitoring:** Track personal or team performance in completing orders.


### **6. Customer Service Staff**
- **Manage Complaint:** View complaints,  handle customer complaints
- **Manage Order :** View order, Update order

## Features
- RESTful APIs for full order lifecycle (create, update, payment, quality check)
- Role-based JWT Authentication
- PayOS QR & bank-transfer payment integration
- Mapbox for address and geolocation flows
- Backblaze B2 storage with signed URLs
- Dockerized deployment with Nginx reverse proxy
- GitHub Actions CI/CD pipeline

## Deployment
The backend is containerized using Docker and deployed to a VPS behind Nginx reverse proxy.  
GitHub Actions automates build, test, and deployment with zero-downtime rolling updates.

## API Documentation
Swagger UI available at: