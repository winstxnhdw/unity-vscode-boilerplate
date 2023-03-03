# unity-vscode-boilerplate

Unity boilerplate with first-class tooling support for [Visual Studio Code](https://code.visualstudio.com/) and built-in support for NuGet packages.

## Requirements

- [.NET Core SDK](https://dotnet.microsoft.com/en-us/download)
- [.NET Desktop Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)
- [C# for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)

## Setup

You can enable Unity warnings for Visual Studio Code by running the following.

```bash
dotnet restore .vscode
```

## Development

### Add NuGet Packages

`UnityNuGet` is a native, fast and lightweight NuGet client wrapper for Unity. Powered by the .NET CLI, say goodbye to the [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) bloatware!

```bash
dotnet add UnityNuGet package <package-name>
dotnet publish UnityNuGet
```

### Housekeeping

If you have removed many Unity packages and you are facing some issues in your editor, you may find it useful to remove all ignored files/directories.

```bash
git clean -fdX
```
