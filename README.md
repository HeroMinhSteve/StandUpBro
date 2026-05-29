# StandUpBro 🧍

A lightweight, strictly-enforced stand-up reminder for Windows desktop, built with C# and WPF.

## Why StandUpBro?
Most stand-up timers just show a polite notification that's easy to swipe away or ignore. StandUpBro is different. When the timer expires, it throws up a borderless, "always on top" window with a randomly generated math challenge.

**You cannot dismiss the window with the 'X' button, the taskbar, or even Alt+F4.** The only way to get back to work is to solve the math problem — forcing you to break your focus and actually take a second to stand up!

## Features

- **Strict Focus Breaker:** Un-dismissable math challenge (addition, subtraction, multiplication) when the timer ends.
- **System Tray Integration:** Runs quietly in your system tray without cluttering your taskbar.
- **Zero-CPU Background Mode:** When minimized to the tray, UI updates are completely suspended to save resources.
- **Silent Autostart:** Optionally launch quietly with Windows. The app defers starting the timer by 10 seconds to avoid competing with other boot processes.
- **Fully Resizable:** Clean, modern dark-mode UI that scales smoothly.

## Building from Source

You'll need the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# Build and run in debug mode
dotnet run --project StandUpBro\StandUpBro.csproj

# Publish as a standalone, single-file executable (no .NET installation required to run)
dotnet publish StandUpBro\StandUpBro.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o .\publish
```

The compiled standalone executable will be located at `.\publish\StandUpBro.exe`.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
