# macOS DMG Troubleshooting Guide

## Common DMG Issues and Solutions

### "DMG file cannot be opened" or "No mountable filesystems"

**Most Common Cause:** Missing or invalid code signing

**Solutions:**

#### 1. For Development/Testing (Quick Fix)
The workflow now includes ad-hoc signing which should resolve basic opening issues. If you're still having problems:

**For end users:**
- Right-click the app → "Open" (instead of double-clicking)
- Go to System Preferences → Security & Privacy → "Open Anyway"
- Or in Terminal: `sudo spctl --master-disable` (re-enable with `--master-enable`)

#### 2. For Distribution (Proper Fix)

##### Step 1: Get a Developer ID Certificate
1. Join the Apple Developer Program ($99/year)
2. Create a "Developer ID Application" certificate
3. Download and install in Keychain Access
4. Export as .p12 file

##### Step 2: Configure Build Pipeline
In the Unity Build Pipeline window:

1. **Enable macOS Signing**: ✓
2. **P12 Certificate Path**: Path to your exported .p12 file
3. **P12 Password**: Password for the .p12 file
4. **Bundle Identifier**: Unique identifier (e.g., `com.yourcompany.yourapp`)
5. **Enable Notarization**: ✓ (recommended)
6. **Team ID**: Your Apple Developer Team ID
7. **Apple ID**: Your Apple Developer account email
8. **App Password**: App-specific password from appleid.apple.com

##### Step 3: Entitlements
Unity apps need specific entitlements. The build pipeline creates these automatically:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
    <key>com.apple.security.cs.disable-executable-page-protection</key>
    <true/>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.allow-dyld-environment-variables</key>
    <true/>
</dict>
</plist>
```

### Other Common Issues

#### "App is damaged and can't be opened"
- **Cause**: Corrupted download or quarantine attribute
- **Solution**: In Terminal: `xattr -dr com.apple.quarantine /path/to/your/app.app`

#### "App can't be opened because Apple cannot check it for malicious software"
- **Cause**: App is not notarized
- **Solution**: Enable notarization in build pipeline or use the right-click → Open method

#### "App unexpectedly quit" or crashes on launch
- **Cause**: Missing dependencies, wrong architecture, or permissions
- **Solutions**:
  1. Check Console.app for crash logs
  2. Verify app architecture: `file /path/to/app.app/Contents/MacOS/appname`
  3. Check for missing frameworks: `otool -L /path/to/app.app/Contents/MacOS/appname`

#### DMG appears empty when mounted
- **Cause**: Incorrect DMG creation or app bundle structure
- **Solution**: The new validation step should catch this. Check workflow logs for details.

## Manual Verification Steps

### 1. Check App Bundle Structure
```bash
# Should show proper app bundle structure
ls -la YourApp.app/Contents/
# Should contain: Info.plist, MacOS/, Resources/, etc.
```

### 2. Verify Code Signature
```bash
# Check if signed
codesign -dv --verbose=4 YourApp.app

# Verify signature
codesign --verify --deep --strict --verbose=2 YourApp.app
```

### 3. Test App Launch
```bash
# Try launching from command line to see error messages
open YourApp.app
# Or directly:
./YourApp.app/Contents/MacOS/YourApp
```

### 4. Check Quarantine
```bash
# Check if quarantined
xattr YourApp.app

# Remove quarantine if needed
xattr -dr com.apple.quarantine YourApp.app
```

## Understanding macOS Security Requirements

### macOS Versions and Requirements

| macOS Version | Requirement |
|---------------|-------------|
| 10.14 (Mojave) | Code signing recommended |
| 10.15 (Catalina) | Code signing required, notarization for downloaded apps |
| 11.0+ (Big Sur+) | Code signing + notarization required for all external apps |

### Code Signing Types

1. **Ad-hoc signing** (`--sign -`): Basic functionality, security warnings
2. **Developer ID signing**: No warnings for identified developers
3. **Developer ID + Notarization**: No warnings, fastest user experience

## GitHub Actions Workflow Details

The DMG creation workflow now includes:

1. **Improved Permission Setting**: Sets executable permissions on all critical files
2. **Ad-hoc Code Signing**: Provides basic signature for functionality
3. **DMG Validation**: Tests if the created DMG can be mounted and contains a valid app
4. **Detailed Logging**: Better error reporting and diagnostics

## Quick Checklist for Users

**When downloading a DMG that won't open:**

- [ ] Try right-click → Open instead of double-clicking
- [ ] Check System Preferences → Security & Privacy for blocked app notification
- [ ] Try removing quarantine: `xattr -dr com.apple.quarantine Downloaded.dmg`
- [ ] Verify download wasn't corrupted (check file size against expected)

**For developers distributing DMGs:**

- [ ] Join Apple Developer Program
- [ ] Set up proper code signing in build pipeline
- [ ] Enable notarization
- [ ] Test on clean macOS system before release
- [ ] Include installation instructions for users

## Getting Help

If you're still having issues:

1. Check the GitHub Actions workflow logs for specific error messages
2. Run the manual verification steps above
3. Check macOS Console.app for crash reports or security messages
4. Consider using the ZIP distribution as a fallback while resolving signing issues

The ZIP file created alongside the DMG should work with manual extraction and the right-click → Open method. 