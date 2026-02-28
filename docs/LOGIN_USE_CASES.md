# Login Use Cases

## UC-L1: Successful login with email and password
- **Actor:** Registered user
- **Preconditions:**
  - User account exists and is active.
  - User has verified their email address.
- **Trigger:** User selects **Log in** on the sign-in page.
- **Main flow:**
  1. User enters email and password.
  2. System validates input format.
  3. System authenticates credentials.
  4. System creates a secure session/token.
  5. System redirects user to the dashboard.
- **Postconditions:**
  - User is authenticated.
  - Active session is stored.
- **Acceptance criteria:**
  - Error-free login completes in under 2 seconds for normal load.
  - Dashboard is shown after successful authentication.

## UC-L2: Failed login due to invalid credentials
- **Actor:** Registered user
- **Preconditions:**
  - User account exists.
- **Trigger:** User submits incorrect email/password combination.
- **Main flow:**
  1. User enters email and password.
  2. System attempts authentication.
  3. System detects mismatch.
  4. System displays generic error message (e.g., "Invalid credentials").
- **Alternative flow:**
  - After 5 failed attempts, system temporarily locks login for 15 minutes.
- **Postconditions:**
  - User remains unauthenticated.
  - Failed attempt is recorded for security monitoring.
- **Acceptance criteria:**
  - Message does not reveal whether email or password was wrong.
  - Lockout policy is enforced consistently.

## UC-L3: Login with multi-factor authentication (MFA)
- **Actor:** Registered user with MFA enabled
- **Preconditions:**
  - User account exists and MFA is configured.
- **Trigger:** User submits valid primary credentials.
- **Main flow:**
  1. User enters valid email and password.
  2. System prompts for second factor (authenticator app or OTP).
  3. User provides MFA code.
  4. System validates the MFA code.
  5. System creates authenticated session and redirects user.
- **Alternative flow:**
  - If MFA code is invalid or expired, system denies access and allows retry.
- **Postconditions:**
  - User is authenticated only after both factors are valid.
- **Acceptance criteria:**
  - MFA challenge appears only after correct primary credentials.
  - Expired OTPs are rejected with clear guidance.

## UC-L4: Forgot password and login recovery
- **Actor:** Registered user
- **Preconditions:**
  - User knows account email.
- **Trigger:** User selects **Forgot password** on login page.
- **Main flow:**
  1. User enters registered email.
  2. System sends password reset link/token.
  3. User opens link and sets new password.
  4. System confirms password update.
  5. User logs in with new password.
- **Postconditions:**
  - Password is replaced and old password is invalidated.
- **Acceptance criteria:**
  - Reset link expires after configured time window (e.g., 30 minutes).
  - User receives confirmation after successful reset.

## UC-L5: Session timeout and re-login
- **Actor:** Authenticated user
- **Preconditions:**
  - User has an active session.
- **Trigger:** Session expires due to inactivity.
- **Main flow:**
  1. System detects inactivity timeout.
  2. System invalidates current session.
  3. User attempts a protected action.
  4. System redirects user to login page.
  5. User logs in again and continues work.
- **Postconditions:**
  - Previous session cannot be reused.
  - New authenticated session is required.
- **Acceptance criteria:**
  - Protected endpoints reject expired sessions.
  - User sees clear timeout notification and re-authentication prompt.
