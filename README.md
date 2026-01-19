# DotNetRE
RevInsight is an open-source reverse engineering tool.
=======

RevInsight is a professional, open-source .NET (Also supports native and may other formats) reverse engineering CLI that analyzes, deobfuscates, and unpacks managed assemblies,

## Features

- Spectre.Console CLI with rich tables and status output
- Assembly analysis with entropy and obfuscation heuristics (.NET + native PE)
- Native PE analysis: headers, sections, imports/exports, resources, strings, disassembly stub
- Native Windows executable compatibility (PE32/PE32+)
- de4dot-inspired anti-anti-decompiler pass pipeline
- Deobfuscation modules (strings, resources, constants, renaming)
- Configurable source output formats (cs, single, il)
- ConfuserEx-focused static unpacking
- Anonymous GitHub release update checks
- MIT licensed

## Install

Build from source (recommended):

1. Install the .NET 10 SDK.
2. Build and run:

```bash
dotnet build -c Release
dotnet run --project src/DotNetRE -- analyze ./sample.exe
```

Single-file publish:

```bash
dotnet publish src/DotNetRE -c Release -r linux-x64 /p:PublishSingleFile=true
```

## Build and Publish

### Build from source

```bash
dotnet restore
dotnet build -c Release
```

### Run locally

```bash
dotnet run --project src/DotNetRE -- analyze /path/to/binary
```

### Publish single-file executables

Windows x64:

```bash
dotnet publish src/DotNetRE -c Release -r win-x64 /p:PublishSingleFile=true /p:SelfContained=true
```

Linux x64:

```bash
dotnet publish src/DotNetRE -c Release -r linux-x64 /p:PublishSingleFile=true /p:SelfContained=true
```

macOS x64:

```bash
dotnet publish src/DotNetRE -c Release -r osx-x64 /p:PublishSingleFile=true /p:SelfContained=true
```

The executable will be in:

```
src/DotNetRE/bin/Release/net10.0/<rid>/publish/
```

## Usage

```text
dotnet-re analyze <ASSEMBLY> [--check-update]
dotnet-re deobfuscate <ASSEMBLY> [--format cs|single|il] [--auto] [--output <DIR>] [--check-update]
dotnet-re unpack <ASSEMBLY> [--auto] [--output <DIR>] [--check-update]
dotnet-re update [--output <DIR>]
```

Example (native PE):

```bash
dotnet run --project src/DotNetRE -- analyze /workspaces/DotNetRE/Testing/Wave.exe
```

### Output

Each run emits output into a timestamped folder under `./output` by default:

```
output/20260118-173012/
	assembly/
	source/
	unpack/
```

Use `--output <DIR>` to override the root folder. DotNetRE always creates a new timestamped subfolder per run.

### Deobfuscation prompt

When obfuscation is detected, DotNetRE prompts before applying deobfuscation. Use `--auto` to proceed automatically (defaults to analyze + deobfuscate).

## Legal

DotNetRE is intended for lawful reverse engineering, security research, and interoperability. You are responsible for complying with local laws and licenses. See DISCLAIMER.md for details.

## License

MIT. See LICENSE.
>>>>>>> a54efab (Initial commit)
