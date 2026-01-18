using Spectre.Console;

namespace DotNetRE.Core;

public static class ConsoleUi
{
    public static void RenderBanner()
    {
        var figlet = new FigletText("DotNetRE")
            .Centered()
            .Color(Color.Cyan1);
        AnsiConsole.Write(figlet);
        AnsiConsole.MarkupLine("[bold cyan]DotNetRE[/] [grey](Reverse Engineering CLI)[/]");
        AnsiConsole.MarkupLine("[grey]For lawful reverse engineering, research, and interoperability only.[/]");
        AnsiConsole.WriteLine();
    }

    public static void RenderWarning()
    {
        AnsiConsole.MarkupLine("[yellow]Use responsibly. You are responsible for compliance with local laws and licenses.[/]");
        AnsiConsole.WriteLine();
    }
}
