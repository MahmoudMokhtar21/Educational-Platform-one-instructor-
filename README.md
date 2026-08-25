# 🎓 Educational Platform

A scalable **Educational Platform RESTful API** built with **ASP.NET Core Web API and MongoDB**. The platform provides secure authentication, course and lesson management, student enrollments, online payments, file uploads, and course statistics.

---

## 🚀 Features

### 🔐 Authentication & Authorization

* User registration and login
* JWT-based authentication
* Role-based authorization
* Email verification
* Password recovery and reset
* Secure password hashing

### 📚 Course Management

* Create and update courses
* Course publishing and status management
* Course categories and tags
* Course search and filtering
* Pagination
* Course thumbnail and trailer uploads
* Course statistics
* Student enrollment tracking

### 📖 Lesson Management

* Create, update, and manage lessons
* Lesson ordering
* Text-based lesson content
* Video uploads
* Video duration tracking
* Preview lessons
* Lesson resources and file uploads

### 👨‍🎓 Student Features

* Enroll in courses
* Access enrolled course content
* Track course-related information
* View available courses

### 💳 Payments

* Stripe payment integration
* Secure payment processing
* Stripe Webhooks
* Payment event handling
* Enrollment after successful payment

### 📊 Course Statistics

* Total students
* Enrollment tracking
* Course-related statistics

---

## 🛠️ Technologies

| Technology               | Purpose              |
| ------------------------ | -------------------- |
| **C#**                   | Programming Language |
| **ASP.NET Core Web API** | Backend Framework    |
| **MongoDB**              | Database             |
| **MongoDB.Driver**       | MongoDB Integration  |
| **JWT**                  | Authentication       |
| **BCrypt**               | Password Hashing     |
| **Stripe**               | Payment Processing   |
| **AutoMapper**           | Object Mapping       |
| **Swagger / OpenAPI**    | API Documentation    |

---

## 🏗️ Architecture

The project follows a structured backend architecture designed to keep business logic separated from API controllers and data access.

```text
Educational Platform
│
├── Controllers
│   ├── AuthController
│   ├── CoursesController
│   ├── LessonsController
│   ├── EnrollmentsController
│   └── PaymentsController
│
├── Services
│   ├── AuthService
│   ├── CourseService
│   ├── LessonService
│   ├── EnrollmentService
│   └── PaymentService
│
├── Repositories
│   ├── UserRepository
│   ├── CourseRepository
│   ├── LessonRepository
│   └── EnrollmentRepository
│
├── Models
├── DTOs
├── Mapping
├── Configuration
└── Program.cs
```

---

## 🔑 Authentication

The API uses **JWT Bearer Authentication**.

After successful login, the client receives a JWT token that must be included in protected requests:

```http
Authorization: Bearer YOUR_TOKEN
```

Authorization is handled using user claims and roles.

---

## 💳 Stripe Integration

The platform integrates **Stripe** to process course payments.

The payment flow uses:

```text
Student
   │
   ▼
Create Payment
   │
   ▼
Stripe
   │
   ▼
Payment Successful
   │
   ▼
Stripe Webhook
   │
   ▼
Backend Verification
   │
   ▼
Student Enrollment
```

Stripe Webhooks are used to reliably handle payment events on the backend.

---

## 📁 File Uploads

The platform supports uploading educational content such as:

* Course thumbnails
* Course trailers
* Lesson videos
* Lesson resources

Uploaded files are associated with their corresponding courses or lessons through stored URLs.

---

## ⚙️ Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/your-username/educational-platform.git
```

### 2. Navigate to the project

```bash
cd educational-platform
```

### 3. Configure MongoDB

Add your MongoDB configuration to `appsettings.json` or preferably use **User Secrets / environment variables** for sensitive information.

Example:

```json
{
  "MongoDb": {
    "ConnectionString": "YOUR_MONGODB_CONNECTION_STRING",
    "DatabaseName": "EducationalPlatform"
  }
}
```

### 4. Configure JWT

```json
{
  "Jwt": {
    "SecretKey": "YOUR_SECRET_KEY",
    "Issuer": "YOUR_ISSUER",
    "Audience": "YOUR_AUDIENCE",
    "ExpiryMinutes": 60
  }
}
```

### 5. Configure Stripe

Add your Stripe configuration securely:

```json
{
  "Stripe": {
    "SecretKey": "YOUR_STRIPE_SECRET_KEY",
    "WebhookSecret": "YOUR_WEBHOOK_SECRET"
  }
}
```

> ⚠️ Never commit real API keys, JWT secrets, database credentials, or webhook secrets to GitHub.

### 6. Restore dependencies

```bash
dotnet restore
```

### 7. Run the application

```bash
dotnet run
```

---

## 📖 API Documentation

After running the application, Swagger provides interactive API documentation.

```text
https://localhost:{port}/swagger
```

Swagger can be used to:

* Explore available endpoints
* Send API requests
* Test authentication
* Test protected endpoints
* Inspect request and response models

---

## 🔒 Security

The project implements several security practices:

* JWT authentication
* Role-based authorization
* Password hashing with BCrypt
* Input validation
* Protected payment webhooks
* Secure handling of sensitive configuration

Sensitive credentials should always be stored using environment variables, User Secrets, or a secure secrets manager.

---

## 📌 Project Status

🚧 **In Development**

The backend API is being developed as a production-oriented educational platform and will be integrated with a modern frontend application.

---

## 👨‍💻 Author

**Mahmoud Moukhtar**

Backend Developer focused on **C#, ASP.NET Core, RESTful APIs, MongoDB, and backend architecture**.
