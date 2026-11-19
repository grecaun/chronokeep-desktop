# Chronokeep

Race timing software for chip timed running events and other timed events. Chronokeep reads chips from timing
systems, works out results as runners cross each timing location, and shares them live on the web, an
announcer's screen, and in printed or exported results.

It runs on Windows, macOS and Linux.

## Timing systems

- **RFID Ultra** readers
- **IPICO** and **IPICO Lite** readers
- **Chronokeep Portal**, which can also pass on readers' battery, temperature and other warnings
- **Remote reads** downloaded from a Chronokeep Remote server, for readers placed out on the course
- A **UHF RFID desk reader**, through the Dashboard's Tag Tester, which shows whose chip it is in large text

Manual times can be entered for anyone without a chip.

## What it does

- **Events:** distance based races, time based events and backyard ultras, with distances (including linked
  distances), timing locations, segments, age groups, divisions and waves.
- **Participants:** import from CSV or Excel, download from Runsignup or a Chronokeep results site, or add them
  through the Chronokeep Registration app. Bibs and chips can be linked one by one or in ranges, and conflicts
  between entries are highlighted for sorting out.
- **Timing:** live chip reads from every connected system, repeat read handling, DNS mode, DNF entries with the
  location a runner dropped, rewinding reads from a reader, and running clocks on Chronoclock displays.
- **Results:** ranked by clock time, chip time or a mix, with overall, gender, age group and division
  placements. Results upload live to a Chronokeep results website or any server running the
  [Chronokeep Results API](https://github.com/grecaun/chronokeep-results).
- **Announcer:** a web page any phone, tablet or computer on the network can open, listing runners as they pass
  the announcer location and finish, with distance clocks, light and dark themes, and a full screen mode that
  keeps the screen awake.
- **Output:** printable results and awards (as PDF), certificate emails through Mailgun, and exports for
  RunSignup, UltraSignup, Boston and Abbott World Marathon Majors qualifiers. Exports are CSV by default, with
  Excel available.

Help pages for each part of the program are in the `help` folder, linked from the About page.

## Data

The database, logs and exports are kept in a folder every user on the computer can use:

| System | Folder |
|---|---|
| Windows | `Public Documents\Chronokeep` |
| macOS | `/Users/Shared/Chronokeep` |
| Linux | `/var/lib/chronokeep` |

On Linux an administrator needs to create that folder first; Chronokeep explains how if it's missing.

## Installing

Download the latest release from GitHub:

- **Windows:** the setup program
- **macOS:** the `.dmg`
- **Linux:** the `.deb` or `.rpm` package

Each release lists SHA256 checksums, and each file has a GitHub build attestation. Chronokeep checks for updates
itself, and only installs an update whose checksum matches and that GitHub confirms was built by Chronokeep's
release process.

Printing results and awards to PDF uses [WeasyPrint](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html).
It comes with Chronokeep on Windows. On macOS and Linux install it separately; the Linux packages recommend it.

## Building

Chronokeep is a .NET 10 program built with [Avalonia](https://avaloniaui.net/).

```sh
dotnet build Chronokeep.sln
dotnet run --project Chronokeep
```

To build a self-contained copy for one system (`win-x64`, `osx-x64` or `linux-x64`):

```sh
dotnet publish Chronokeep/Chronokeep.csproj -c Release -r linux-x64 --self-contained true -p:IsPublishable=true -o publish/linux-x64
```

### Tests

```sh
dotnet test Chronokeep.test
```

The tests cover the database, results processing, the timing systems' messages, imports and exports, the
network APIs (against local fake servers), and the user interface (run headless with Avalonia). They take about
ten minutes.

## Releases

Pushing a tag runs `.github/workflows/deploy.yml`, which builds the Windows installer (with Inno Setup and
`install-script.iss`), the Linux `.deb` and `.rpm` (with nfpm, from `packaging/linux`) and the macOS `.dmg`
(from `packaging/macos`), then uploads them to the tag's release with a `SHA256SUMS` file and build
attestations. Changes for each version are listed in `Chronokeep/changelog/<version>.json`.

## License

GNU Affero General Public License v3. See [LICENSE](LICENSE).
