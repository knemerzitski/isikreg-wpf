# Personnel Registration Application

Personnel Registration Application is used for **quick registration** of personnel
using **Estonian ID-cards**. It manages personnel registration in a searchable table.

This app is a port of my original [Java version](https://github.com/knemerzitski/isikreg-javafx) to C#. 
I wanted to make a version that doesn't rely on Java and C# has a similar syntax. Besides it's always fun to learn a new language.

![Personnel Registration Application](assets/hero.jpg)

## Getting Started

Windows 7 or higher with [.NET 7.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) is required to run this application.

## Build

This project can be easily compiled with Visual Studio.

1. Open project with Visual Studo 
2. Make sure solution configuration is `Release` and `Any CPU`
3. In menu select `Build > Build Solution`
4. Compiled application with all dependencies is at `./bin/Release/net7.0-windows/`

## Key Features
- Estonian ID-card support using PC/SC
- Supports multiple concurrent card readers
- Separate feedback window per card reader
- Excel file import / export
- Auto merge multiple excel files
- Fully customizable table columns and settings

## Documentation
- [User Manual](docs/isikreg_kasutusjuhend_4.2.pdf)
- [Alert Quick Start Manual](docs/isikreg_häire_lühijuhend_4.2.pdf)
- [Settings Documentation](docs/isikreg_seadete_dokumentatsioon_4.2.pdf)
- [Settings Examples](docs/seadete_näited)
- [Version History](docs/versiooni_ajalugu.md)

## Technologies
- C# (.NET 7.0)
- Windows Presentation Foundation (WPF)
- XAML
- PC/SC
- Excel

## License
Personnel Registration Application is MIT licensed.