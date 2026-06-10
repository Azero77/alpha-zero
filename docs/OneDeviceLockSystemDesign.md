✦ This report details the implementation of the Cryptographic Device Locking & Tracking feature within
  the AlphaZero IAM (Identity & Access Management) framework.

  📄 Feature Executive Summary (Plain Language)

  The goal of this feature is to prevent account sharing—where one student pays for a course and
  shares their login with others.

  In most apps, "device locking" is just a simple ID saved in the browser that can be easily copied.
  In AlphaZero, we have implemented Cryptographic Binding. Every time a student’s device talks to our
  servers, it must "sign" the request using a private key that never leaves the device's secure
  storage.

  How it works for the user:
   1. Registration: When the student first logs in, the app creates a unique digital signature for
      that phone or laptop.
   2. The "Main Device": The student designates one device (e.g., their iPhone) as the "Main Device."
   3. The Lock: If a course is marked as "locked," the student can only watch videos or take exams
      from that specific iPhone. If they try to log in from a friend’s phone or a web browser, the
      system will recognize it isn't the "Main Device" and block access.
   4. Anti-Cheating Cooldown: To prevent students from just switching their "Main Device" every hour
      to share accounts, they are only allowed to change their Main Device once every 90 days.

  ---

  🏛️ System Design & Architectural Rationale

  This feature was built using a Zero-Trust and ABAC (Attribute-Based Access Control) approach.

  1. Why Cryptographic Key-Pairs?
  Instead of trusting a DeviceId header (which is easily spoofed), we use Asymmetric Encryption
  (RSA/ECDSA).
   * The Private Key: Stays in the device’s hardware-backed Keystore/Keychain. Even the student cannot
     "see" it to copy it.
   * The Public Key: Stored in our database.
   * The Signature: For every request, the device signs the Path + Timestamp. This proves the device
     is physically present and prevents Replay Attacks (where an attacker captures a valid signature
     and tries to use it again later).

  2. Why a custom "IsMainDevice" Operator?
  We integrated this into our existing Policy Engine. Rather than hard-coding checks in every
  controller, we added a new logic piece to our "Security Brain":
   * The Operator: IsMainDevice
   * The Logic: It compares the DeviceId of the current request with the UserMainDeviceId stored in
     the user’s profile.
   * The Benefit: This makes the system extremely flexible. A school can decide that "Math 101" is
     locked to a device, but "History 101" is open to all devices, simply by changing a JSON policy.

  3. High-Performance Caching Layer
  Verifying a cryptographic signature and checking a database on every single API call is expensive
  and slow.
   * The Solution: We implemented a CachePublicKeyProvider.
   * How it behaves: We use a high-performance Hybrid Cache (Redis + In-Memory). The first time a
     device connects, we fetch the Public Key from the DB and cache it for 24 hours. Subsequent
     requests are verified in microseconds without hitting the database.

  ---

  🛠️ Technical Components Diagram

    1 [ Mobile/Web Client ]
    2        |
    3        | 1. Request + X-Signature + X-Timestamp
    4        v
    5 [ DeviceSignatureValidator (Pre-Processor) ]
    6        |
    7        | 2. Fetch Public Key from HybridCache (or DB)
    8        | 3. Verify Signature (RSA SHA256)
    9        | 4. Verify Timestamp (Anti-Replay)
   10        v
   11 [ IAMPreprocessor ]
   12        |
   13        | 5. Create AuthorizationContext (Includes CurrentDeviceId + UserMainDeviceId)
   14        v
   15 [ PolicyEvaluationEngine ]
   16        |
   17        | 6. Evaluate Policy Conditions:
   18        |    IF (Operator == IsMainDevice)
   19        |       RETURN (CurrentDeviceId == UserMainDeviceId)
   20        v
   21 [ Access Granted/Denied ]

  📈 System Benefits
   * Integrity: Guaranteed 1:1 relationship between a subscription and a physical device.
   * Scalability: The caching mechanism ensures the Identity module doesn't become a bottleneck as the
     user base grows.
   * Maintainability: The use of IDeviceProvider and IDeviceSignatureVerifier interfaces keeps the
     code "Clean" and easy to unit test without needing a database or real HTTP context.
   * User Protection: Even if a student's password is stolen, the attacker cannot access locked
     content because they don't have the physical device's private key.
