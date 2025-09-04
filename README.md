# Yubikey Verification Monorepo

## Overview

This project provides a secure solution for verifying user identities using YubiKeys and Okta. It consists of a frontend and backend, enabling users to submit YubiKey OTPs for validation against an Okta instance. Designed for service desk scenarios, it allows verification both inside and outside the corporate network, with cloud-edge deployment for broad accessibility.

## Architecture

- **Frontend**: Vue.js app for users to submit YubiKey OTPs.
- **Backend**: .NET API for validating OTPs against Okta and managing verification sessions.

## Intended Audience

Service desk teams requiring users to verify their identities with YubiKeys, regardless of network location. The service is suitable for cloud-edge deployment, supporting both internal and public access.

## Technologies Used

- **Frontend**: Vue.js, Vite, Tailwind CSS, Node.js v20
- **Backend**: .NET 8, SignalR, SQL Server, Okta integration

## Setup Instructions

### Frontend

1. Navigate to `Yubikey-frontend/yubikey-verify-app/`
2. Install dependencies:
   ```powershell
   npm install
   ```
3. Ensure Node.js v20 is installed.

### Backend

1. Ensure you have a SQL Server instance available.
2. Create and configure `appsettings.json` (not included in repo) with your database and Okta credentials.
3. Apply database migrations:
   ```powershell
   dotnet ef database update
   ```
4. Build and run the backend:
   ```powershell
   dotnet build
   dotnet run
   ```

## Configuration

- The backend requires an `appsettings.json` file with connection strings and Okta settings.
- The frontend and backend can be deployed separately or together, depending on your infrastructure.

## Contributing

Contributions are welcome! Please open issues or submit pull requests for improvements.

