# macOS DMG Code Signing Setup

This document explains how to set up proper code signing for macOS DMG creation using the build pipeline.

## Overview

The build pipeline can create macOS DMG files with proper code signing instead of ad-hoc signing. This eliminates security warnings for end users.

## Required GitHub Secrets

To enable proper code signing in the DMG creation workflow, you need to set up the following GitHub repository secrets:

### 1. P12_CERT (Recommended) or P12_CERTIFICATE
- **Description**: Your Developer ID Application certificate exported as a base64-encoded P12 file
- **Default Name**: The build pipeline defaults to `P12_CERT` but supports both naming conventions
- **How to get it**:
  1. Export your Developer ID Application certificate from Keychain Access as a `.p12` file
  2. Convert to base64: `base64 -i certificate.p12 | pbcopy`
  3. Paste the result into this GitHub secret
- **Flexible Configuration**: You can configure which secret name to use in the build pipeline settings

### 2. P12_PASSWORD (Optional)
- **Description**: The password you used when exporting the P12 certificate
- **When Required**: Only needed if your P12 certificate has a password
- **Configuration**: Mark "Certificate has password" in the build pipeline if your certificate requires one
- **Security**: Store this securely in GitHub Secrets

### 3. APPLE_ID_PASSWORD (Optional - for notarization)
- **Description**: App-specific password for your Apple ID
- **How to get it**:
  1. Go to https://appleid.apple.com
  2. Sign in with your Apple ID
  3. Generate an app-specific password for command-line tools
  4. Store this password in GitHub Secrets

## How It Works

1. **Build Pipeline Configuration**: Configure GitHub secrets usage and certificate settings in the Unity build pipeline window
2. **Automatic Secret Detection**: The workflow automatically detects and uses the appropriate certificate secret (`P12_CERT` or `P12_CERTIFICATE`)
3. **Timeout Protection**: All signing operations include timeout protection to prevent hanging (30-120 seconds)
4. **Graceful Fallback**: If Developer ID signing fails, automatically falls back to ad-hoc signing
5. **Proper Signing**: The GitHub Actions workflow uses your certificates to properly sign the macOS app
6. **Notarization**: If configured, the app is also notarized by Apple after successful signing
7. **Distribution Ready**: DMG files are created with proper permissions and distribution-friendly attributes

## Configuration Steps

### 1. In Unity Build Pipeline Window
- ✅ Enable "Sign macOS Build" 
- 🔐 Enable "Use GitHub Secrets" (recommended for CI/CD)
- 📝 Configure "P12 Secret Name" (defaults to "P12_CERT")
- 🔑 Check "Certificate has password" if your P12 file requires a password
- 📱 Set Bundle Identifier
- 🍎 Enable Notarization (if desired)
- 👤 Set Team ID and Apple ID (for notarization)

**Local Development Alternative**:
- 📝 Set P12 Certificate path (for local builds only)
- 🔑 Set P12 Password (for local builds only)

### 2. In GitHub Repository
- 🔐 Add `P12_CERT` secret (base64-encoded P12 file) - or use `P12_CERTIFICATE` if preferred
- 🔑 Add `P12_PASSWORD` secret (only if your certificate requires a password)
- 🍎 Add `APPLE_ID_PASSWORD` secret (if using notarization)

### 3. Build and Release
- 🔧 Run the build pipeline with GitHub release enabled
- 📦 The DMG will be created with proper signing automatically

## Troubleshooting

### ❌ "timeout: command not found" 
- **Fixed**: The workflow now automatically installs GNU coreutils
- This error should no longer occur with the updated workflow

### ❌ "Certificate import timed out or failed"
- Check that your P12 file is valid and properly base64-encoded
- Verify the P12 password is correct (if required)
- Check that "Certificate has password" setting matches your certificate
- The workflow includes 30-second timeout protection

### ❌ "No Developer ID Application identity found"
- Check that your P12 file contains a Developer ID Application certificate
- Verify the P12 password is correct
- Ensure the certificate hasn't expired
- Verify the secret name matches your configuration (`P12_CERT` vs `P12_CERTIFICATE`)

### ❌ "Code signing failed or timed out"
- The workflow includes 120-second timeout protection for signing operations
- Check GitHub Actions logs for specific error details
- Verify certificate is valid and not expired
- The workflow will automatically fall back to ad-hoc signing

### ❌ "Notarization failed"
- Verify your Apple ID and app-specific password are correct
- Check that your Team ID is correct
- Ensure your Apple Developer account has notarization permissions
- Notarization only runs after successful Developer ID signing

### ✅ "DMG creation falls back to ad-hoc signing"
- **This is normal behavior** when Developer ID signing fails
- Check GitHub Actions logs for why Developer ID signing failed
- Ad-hoc signing still creates functional DMG files
- Users will see standard macOS security warnings

### ⚠️ "App can't be opened because it is from an unidentified developer"
- This happens with ad-hoc signing or unsigned apps
- **User Solution**: Right-click → Open, or System Preferences → Security & Privacy → Allow
- **Developer Solution**: Use proper Developer ID signing and notarization for production

## Testing

1. **Local Testing**: First test signing locally in the build pipeline
2. **GitHub Actions**: Check the workflow logs to see if signing succeeded
3. **Download and Test**: Download the DMG and test on a clean macOS system
4. **Permission Testing**: DMG files now have proper permissions set for distribution
   - Files should open without permission errors
   - Compatible across different macOS user accounts
   - Reduced quarantine warnings on download

## Security Notes

- 🔐 Never commit certificates or passwords to your repository
- 🔑 Always use GitHub Secrets for sensitive information
- 🔄 Rotate app-specific passwords periodically
- 📅 Monitor certificate expiration dates

## Additional Resources

- [Apple Developer Documentation](https://developer.apple.com/documentation/security/notarizing_macos_software_before_distribution)
- [GitHub Secrets Documentation](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
- [Unity macOS Build Settings](https://docs.unity3d.com/Manual/class-PlayerSettingsStandalone.html) 
