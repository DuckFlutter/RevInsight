# RevInsight

RevInsight is a professional, open-source reverse engineering CLI focused on analysis, deobfuscation, and unpacking of managed and native binaries.

## Features

- Spectre.Console-based CLI with structured tables and status output
- Managed (.NET) assembly analysis with entropy checks and obfuscation heuristics
- Native PE analysis:
  - Headers and sections
  - Imports and exports
  - Resources and strings
  - Basic disassembly stubs
- Native Windows executable support (PE32 / PE32+)
- de4dot-inspired anti–anti-decompiler pass pipeline
- Deobfuscation modules:
  - String decryption
  - Resource recovery
  - Constant folding
  - Symbol renaming
- Configurable source output formats:
  - cs (C# project)
  - single (single-file output)
  - il (IL only)
- ConfuserEx-focused static unpacking support

## Installation

### Build from source (recommended)

1. Install the .NET 10 SDK
2. Build the project:

```bash
revinsight restore
revinsight build -c Release
```

Publish single-file executable
Linux x64:
```bash
revinsight publish src/RevInsight -c Release -r linux-x64 /p:PublishSingleFile=true /p:SelfContained=true
```
Windows x64:
```bash
revinsight publish src/RevInsight -c Release -r win-x64 /p:PublishSingleFile=true /p:SelfContained=true
```
macOS x64:
```bash
revinsight publish src/RevInsight -c Release -r osx-x64 /p:PublishSingleFile=true /p:SelfContained=true
```
The resulting executable will be located at:
```
src/RevInsight/bin/Release/net10.0/<rid>/publish/
```

Rename the binary to ``revinsight`` (or ``revinsight.exe`` on Windows) and ensure it is available on your PATH.
Usage
```
revinsight analyze <ASSEMBLY> [--check-update]
revinsight deobfuscate <ASSEMBLY> [--format cs|single|il] [--auto] [--output <DIR>] [--check-update]
revinsight unpack <ASSEMBLY> [--auto] [--output <DIR>] [--check-update]
revinsight update [--output <DIR>]
```
## Examples

Analyze a native PE file:
```
revinsight analyze ./Testing/Testing.exe
```
Analyze and deobfuscate automatically:
```
revinsight deobfuscate ./sample.exe --auto
```
Output

Each execution creates a new timestamped directory under ``./output`` by default:
```
output/20260118-173012/
  assembly/
  source/
  unpack/
```
Use ``--output <DIR>`` to override the root output directory. RevInsight always creates a fresh timestamped subdirectory per run to avoid overwriting results.


## Legal (Refer to DISCLAIMER.md)

RevInsight is intended solely for lawful reverse engineering, security research, educational use, and interoperability testing.
You are responsible for ensuring that your use of this tool complies with all applicable laws, regulations, and software licenses.
