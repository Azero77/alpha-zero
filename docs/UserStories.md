# 🎓 AlphaZero Learning Academy - Full User Stories

## 🧑‍🎓 Student User Stories
### 🎯 Onboarding & Access
- **Tenant Isolation:** As a student, I want to join my specific school/university tenant so that my dashboard is free from unrelated content.
- **Low-Bandwidth Login:** As a student, I want a lightweight login experience so I can access my studies even on a 3G/Edge connection.
- **Library Redemption:** As a student, I want to redeem a physical access code (from a library) so that I can unlock courses without needing a credit card.
- **Subscription Status:** As a student, I want to see which courses are "Active," "Expired," or "Locked" so I can manage my learning schedule.

### 📚 Learning Experience
- **Adaptive Streaming:** As a student, I want video quality to auto-adjust based on my speed so I don't experience buffering.
- **Offline Study:** As a student, I want to download lesson PDFs and notes so I can study during power outages or without internet.
- **Progress Sync:** As a student, I want my video progress to sync.

### 🧮 Assessments & Feedback
- **Automated Quizzes:** As a student, I want to take MCQs and get instant results to test my knowledge quickly.
- **Handwritten Submission:** As a student, I want to upload photos of my handwritten math/essay answers so my teacher can grade my actual work.
- **Correction Review:** As a student, I want to see my teacher’s annotations on my submitted exams so I can understand my mistakes.

### 💬 Engagement & Community
- **Course Community:** As a student, I want to post questions on specific video lessons to get help from the course worker or peers.
- **Peer Motivation:** As a student, I want to see my rank on the tenant leaderboard to stay competitive and motivated.

---

## 🧑‍🏫 Teacher User Stories
### 📝 Content & Validation
- **Exam Authority:** As a teacher, I want to write the core exams and review/approve any questions drafted by my Course Workers to ensure accuracy.
- **Handwritten Grading:** As a teacher, I want a specialized interface to view student photo uploads and assign marks/comments to handwritten work.
- **Course Performance:** As a teacher, I want to see which lessons have the highest "drop-off" rate so I can improve my teaching material.

### 🤝 Interaction
- **Official Guidance:** As a teacher, I want to "Verify" a student's answer in the community so other students know it is correct.
- **Student Intervention:** As a teacher, I want to see a list of "At-Risk" (inactive) students so I can reach out and encourage them.

---

## 🛠️ Course Worker User Stories
### 🏗️ Content Production
- **Bulk Uploading:** As a course worker, I want to upload multiple video lessons and materials on behalf of the teacher.
- **Exam Drafting:** As a course worker, I want to build MCQ and True/False banks for the teacher to review.
- **Community Management:** As a course worker, I want to be the first line of support, answering basic student questions and escalating complex ones to the teacher.

### ⚙️ Operational Control
- **Enrollment Management:** As a course worker, I want the ability to deactivate a student's enrollment if their physical payment was revoked or terms were violated.

---

## 📋 Tenant Course Manager User Stories
### 🛡️ Quality Assurance
- **Content Audit:** As a course manager, I want to review every video and PDF before it goes "Live" to ensure it meets the Academy's branding and quality standards.
- **Tech Assistance:** As a course manager, I want to provide technical support to teachers (e.g., video editing help, LaTeX formatting) to ensure a smooth production flow.
- **Approval Workflow:** As a course manager, I want to "Reject" a lesson with specific feedback so the Worker knows exactly what to fix.

---

## 🏢 Tenant Admin User Stories
### 🏛️ Institutional Management
- **Financial Oversight:** As a tenant admin, I want to see total enrollments and revenue statistics for my specific school.

---

## 🌍 Tenants Manager (Platform Owner)
### 🚀 Platform Scaling
- **Tenant Creation:** As a tenants manager, I want to spin up new school/university tenants in minutes with a single click.
- **White-Labeling:** As a tenants manager, I want to configure the primary colors, logos, and themes for each tenant to match their brand.
- **Global Health:** As a tenants manager, I want a "God-view" dashboard of system-wide usage, storage, and active users across all tenants.

---

## 📚 Library Team User Stories
### 📖 Library Manager
- **Code Minting:** As a library manager, I want to generate batches of 100+ unique access codes for specific courses.
- **Distribution:** As a library manager, I want to track which codes have been "Sold" vs. "Inventory" to prevent theft.

### 📖 Library Worker
- **Retail Point:** As a library worker, I want a fast search to find a code for a student or generate a new one during a physical sale.

### 📖 Library Accountant
- **Inventory Reconciliation:** As a library accountant, I want to audit all code generation logs to ensure no unauthorized codes were created.
- **Course Mapping:** As a library accountant, I want to "Attach" courses to my library's permission list so my workers can sell them.

---

## 🎧 Support User Stories
### 🏢 Tenant Support Employee (School-Level)
- **Moderation:** As a tenant support employee, I want to delete spam or offensive comments from my school’s community.
- **Ticket Resolution:** As a tenant support employee, I want to reset student passwords or fix course access issues.
- **Staff Provisioning:** As a tenant support, I want to create accounts for Teachers, Workers, and Support staff within my school.
- **Subject Mapping:** As a tenant support , I want to define the Grades (e.g., 10th Grade) and Subjects (e.g., Physics) available in my tenant.

### 🌐 App Support (Platform-Level)
- **Global Community:** As an app support member, I want to manage discussions in the "Public Square" (non-tenant areas) where potential customers ask questions.

---

## 🔔 System-Wide Stories (The "Glue")
- **Instant Notifications:** As a user, I want to receive push/email alerts when:
    - My exam is graded (Student).
    - A student submits an exam (Teacher).
    - A course is approved/rejected (Worker).
    - A student asks a question (Teacher/Worker).
- **Audit Logs:** As an admin, I want to see who changed what (e.g., "Who deactivated this enrollment?") for accountability.
