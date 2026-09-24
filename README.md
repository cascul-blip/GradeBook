# GradeBook
Grade book for OBA

## Requirements

- **.NET 8 SDK** — the app, core library, and tests all target `net8.0`.
- A Linux desktop environment (X11 or Wayland) with the usual desktop libraries (fontconfig, X11/GTK libs) — these are present on virtually any Linux desktop install and don't need separate setup. The UI is built with [Avalonia](https://avaloniaui.net/), which is cross-platform (Windows/Linux).
- No external database server is required — grades are stored locally in SQLite via `Microsoft.Data.Sqlite`, which bundles its own native SQLite library.

## Installing the .NET SDK on Linux

### Arch / Omarchy
```sh
sudo pacman -S dotnet-sdk-8.0
```
Arch's plain `dotnet-sdk` package tracks the latest .NET release (e.g. 10), not 8 — installing it alone leaves you able to *build* this project (targeting `net8.0`) but unable to *run* it, since the matching net8.0 runtime isn't installed. Use the versioned `dotnet-sdk-8.0` package instead, or if you already have a newer SDK and just need the runtime to run the app, install `dotnet-runtime-8.0` on its own.

### Debian / Ubuntu
```sh
sudo apt update
sudo apt install dotnet-sdk-8.0
```
(If your distro's repos only carry an older SDK, use Microsoft's [package feed](https://learn.microsoft.com/dotnet/core/install/linux) or the generic script below instead.)

### Fedora
```sh
sudo dnf install dotnet-sdk-8.0
```

### Any distro (official install script)
```sh
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
```
This installs to `~/.dotnet`; add it to your `PATH` (e.g. `export PATH="$HOME/.dotnet:$PATH"`).

Verify the install:
```sh
dotnet --version
```

## Building and running

```sh
./run.sh
```
or directly:
```sh
dotnet build GradeBook.sln
dotnet run --project src/GradeBook.App/GradeBook.App.csproj
```

## Data safety

- **Daily backup:** on the first launch each day, GradeBook saves a copy of the database next to it as `MMddyy-gradebook.db`. Backups are never deleted automatically.
- **Integrity check:** at startup the database is checked. If it's damaged, GradeBook closes without changing it and tells you where the newest backup is.
- **Open on another computer:** a `gradebook.db.lock` file sits next to the database while GradeBook is running. If another computer has it open, you're warned before anything changes.
- **Sync conflicts:** if the sync client left a `gradebook (conflicted copy …).db` in the folder, GradeBook lists it at startup. Such a copy may hold grades that aren't in the main file.
- **Errors:** failed saves are never silent. They show a message, and details go to `error.log` in the GradeBook application-data folder (`%AppData%\GradeBook` on Windows, `~/.config/GradeBook` on Linux).

## Running tests
```sh
dotnet test
```
