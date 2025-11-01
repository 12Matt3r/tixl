# Quick Start Guide

Get TiXL up and running in just 5 minutes! This guide covers the essential steps to start developing with TiXL.

## Prerequisites

Before you begin, ensure you have:
- **Windows 10/11** (primary development platform)
- **.NET 9.0.0 SDK** or later
- **Visual Studio 2022** or **JetBrains Rider**
- **Git**

## 5-Minute Setup

### 1. Clone the Repository
```bash
git clone https://github.com/tixl3d/tixl.git
cd tixl
```

### 2. Build TiXL
```bash
dotnet restore
dotnet build --configuration Release
```

### 3. Run the Editor
```bash
dotnet run --project Editor/Editor.csproj --configuration Release
```

**That's it!** 🎉 TiXL should now be running on your system.

## What Happens Next?

1. **Explore the Interface**: The TiXL editor will open with a default workspace
2. **Create Your First Project**: Start building your motion graphics
3. **Try Example Projects**: Browse the included examples
4. **Read Documentation**: Continue with the [Developer Onboarding Guide](Getting-Started/Developer-Onboarding)

## Common Issues

### Build Fails?
- Ensure .NET 9.0.0 SDK is installed
- Try `dotnet clean && dotnet restore && dotnet build`
- Check that Visual Studio workloads are properly installed

### Editor Won't Start?
- Verify your GPU supports DirectX 11.3+
- Update graphics drivers
- Run as administrator if needed

### Need Help?
- Join our [Discord community](https://discord.gg/YmSyQdeH3S)
- Check the [troubleshooting guide](Getting-Started/Developer-Onboarding#common-issues-and-troubleshooting)
- Review [GitHub Issues](https://github.com/tixl3d/tixl/issues)

## Next Steps

- **[Complete Setup Guide](Getting-Started/Developer-Onboarding)** - Detailed development environment setup
- **[Contribution Guidelines](Getting-Started/Contribution-Guidelines)** - How to contribute to TiXL
- **[Architecture Overview](Architecture/Technical-Architecture)** - Understanding TiXL's design

---

*Quick start complete! Continue with the full developer guide for comprehensive setup instructions.*
