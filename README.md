# Unity GitHub Build Automation

Build automation pipeline for desktop platforms in Unity with versioned GitHub releases.

## Features

- Automated Unity build process for desktop platforms (Windows, macOS, Linux)
- Automated versioning (Major.Minor.Build)
- MacOS build signing
- MacOS .dmg packaging (Using GitHub actions)
- Windows installer creation
- Build .zip bundler
- Versioned GitHub Releases generation
- Update Checker for runtime application updates

### Requirements

- Unity 6.0 or later supporting build profiles.
- Publishing to a private reposititory requires GitHub Personal Access Token (PAT) with `repo` permissions
- [Inno-Setup](https://jrsoftware.org/isinfo.php) installed to create windows installation files.
- Windows, MacOS, Linux build support installed as needed for your Unity version.

## Getting Started

- Install the latest Unity package found in the releases section by dragging it into Unity's Assets folder.
- Select Build > Build Pipeline from the Unity Toolbar to open the build pipeline window.
- Optionally select build profiles for each platform, otherwise will use the project setting's build profile.
- Select the platforms you wish to build for.
- Optionally input information to sign the MacOS build or create a windows installer.
- Once configured click 'Start Build Pipeline' to build for all platforms & upload to GitHub Releases

## Output

- Outputs builds to a 'Builds' folder at the project root. Builds will be in a folder labeled {ProductName-Platform}
- Outputs Releases to a 'Releases' folder at the project root. Releases will be in .zip files with the build folder names inside a folder named after the version.
- GitHub releases will have a new tag created with the current application version. Only Released files will be uploaded, including .iss files and windows installer called {ProductName-Installer}.exe

## Runtime Updates:

- Once a release is on GitHub use updatechecker.cs API to check for updates and auto-download/install.
- Minimum scripting call ConfigureUpdateSource(string url, string token = "") with your repo info. Then call CheckForUpdates(); If an update is found call UpdateApplication().
- Be sure to protect your PAT within Unity if publishing to a private repository.
- Note this will cause your application to restart. (only tested on windows).

## Screenshot

Below is a preview of the Unity build pipeline in action:

![Unity Build Pipeline Screenshot](unity-build-pipeline-screenshot.jpg)

## Contributing

Contributions are welcome! Please open issues or pull requests for enhancements, bug fixes, or questions.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
