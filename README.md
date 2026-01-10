# 🎓 AlphaZero 

> **Empowering students. Elevating teachers. Revolutionizing Syrian education.**

EduSyria is a next-generation **e-learning platform** designed to bring high-quality education, interactive quizzes, and a vibrant learning community to Syrian high school students.  
Built for **low-bandwidth reliability**, **localized payment challenges**, and **real teacher empowerment**.

a tenat-based saas for schools and universities to adopt as their online academy.

---

## 🌟 Vision

To create a digital learning ecosystem where:
- Students **learn, compete, and collaborate**.
- Teachers **share knowledge and earn fairly**.
- Communities **thrive on helping each other** — like a local StackOverflow for education.

---

## 🚀 MVP (Minimum Viable Product)

### 🎯 Core Goals for the MVP
The first version focuses on the **12th grade** national subjects with:
- 🧑‍🏫 **Teacher dashboards** for course & quiz management  
- 🎥 **Reliable video streaming** (AWS S3 + EC2 caching)  
- 🧮 **Quizzes** with all question types: MCQ, true/false, text, and math expressions  
- 🎟️ **Offline payment system** using redeemable access codes  
- 🧠 **Student analytics** — quiz scores, progress, and performance tracking  
- 💬 **Community Q&A per course** (like Reddit/Stack Overflow)  
- 🪙 **Gamification:** Tokens, badges, and leaderboards  
- 🧩 **Role-based access control (RBAC)** with advanced roles:
  - SuperAdmin for the whole app 
  - Admin For tenat
  - Teacher / Co-worker  
  - Student  
  - Library Manager / Worker  
  - Support Employee  
  - Moderator  
  - Auditor  
---
## 🧱 Architecture Overview

| Layer | Description |
|-------|--------------|
| **Frontend** | React & Flutter (Kotlin & Swift for better native performance in the future) |
| **Backend** | ASP.NET Core Web API with JWT Authentication |
| **Storage** | AWS S3 for videos and files |
| **Database** | PostgreSQL (relational data with transactions) & DynamoDB for quizzes|
| **Hosting** | AWS |
| **Auth** | Identity + Role-based access (Admin, Teacher, Student, etc.) |
| **Payments** | Code-based library system (offline sales) + Online Payments using Cards + Banks in syria (Sham Cash, Syriatel Cash, MTN Cash) |
| **Community** | Threaded discussions per course & per tenat |
| **AI Integration (future)** | Smart quiz generation & student progress analytics |
---

## 🧩 MVP Feature Breakdown

| Feature | Description |
|----------|--------------|
| **1. Course Management** | Teachers can create courses, upload videos, add quizzes. |
| **2. Quiz System** | Supports MCQ, True/False, Text, and Math answers (LaTeX-based). |
| **3. Student Dashboard** | Track enrolled courses, scores, and badges. |
| **4. Library Codes System** | Students can redeem prepaid codes for courses. |
| **5. Token Economy** | Students earn or buy tokens to unlock premium quizzes or features. |
| **6. Community Forum** | Ask and answer questions under each course. |
| **7. Admin Control Panel** | Full audit trail, fraud prevention, user and content management. |

---
## 🧑‍💼 Roles Overview

| Role | Capabilities |
|------|---------------|
| **Admin** | Manage users, roles, payments, and logs |
| **Teacher** | Upload courses, create quizzes, track students |
| **Teacher Co-worker** | Assist teacher with uploads and content management |
| **Student** | Enroll in courses, take quizzes, join discussions |
| **Library Manager** | Generate and monitor course access codes |
| **Library Worker** | Sell and redeem access codes |
| **Support Employee** | Handle student and teacher issues |
| **Moderator** | Monitor community discussions |
| **Auditor** | Review all system logs for fraud or errors |
