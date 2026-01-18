using Spectre.Console;
using Spectre.Console.Cli;
using DotNetRE.Commands;
using DotNetRE.Core;

namespace DotNetRE;

public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		var app = new CommandApp();
		app.Configure(config =>
		{
			config.SetApplicationName("dotnet-re");
			config.AddCommand<AnalyzeCommand>("analyze")
				.WithDescription("Analyze a .NET assembly for obfuscation markers.")
				.WithExample(new[] { "analyze", "sample.exe" });

			config.AddCommand<DeobfuscateCommand>("deobfuscate")
				.WithDescription("Run de4dot-inspired anti-anti checks and deobfuscators.")
				.WithExample(new[] { "deobfuscate", "sample.exe", "--format", "cs" });

			config.AddCommand<UnpackCommand>("unpack")
				.WithDescription("Attempt a static unpack (ConfuserEx-focused).")
				.WithExample(new[] { "unpack", "sample.exe" });

			config.AddCommand<UpdateCommand>("update")
				.WithDescription("Check for updates and download the latest release.");
		});

		try
		{
			ConsoleUi.RenderBanner();
			ConsoleUi.RenderWarning();
			return await app.RunAsync(args);
		}
		catch (CommandParseException ex)
		{
			AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
			return -1;
		}
	}
}
