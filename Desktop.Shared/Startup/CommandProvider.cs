using Remotely.Shared.Helpers;
using System.CommandLine;
using CommunityToolkit.Diagnostics;
using Remotely.Desktop.Shared.Enums;

namespace Remotely.Desktop.Shared.Startup;
public static class CommandProvider
{
    /// <summary>
    /// Creates a <see cref="Command"/> for starting the remote control client.
    /// </summary>
    /// <param name="isRootCommand">Whether to create a <see cref="RootCommand"/> or <see cref="Command"/>.</param>
    /// <param name="commandLineDescription">The description for the command.</param>
    /// <param name="commandName">The name used to invoke the command.  Required if not a root command.</param>
    /// <returns></returns>
    public static Command CreateRemoteControlCommand(
        bool isRootCommand,
        string commandLineDescription,
        string commandName = "")
    {
        Command? rootCommand;

        if (isRootCommand)
        {
            rootCommand = new RootCommand(commandLineDescription);
            // -h is --host here, as before: help keeps --help, -? and /?
            foreach (var help in rootCommand.Options.OfType<System.CommandLine.Help.HelpOption>())
            {
                help.Aliases.Remove("-h");
                help.Aliases.Remove("/h");
            }
        }
        else
        {
            Guard.IsNotNullOrWhiteSpace(commandName);
            rootCommand = new Command(commandName, commandLineDescription);
        }

        var hostOption = new Option<string>("--host", "-h")
        {
            Description = "The hostname of the server to which to connect (e.g. https://example.com)."
        };
        rootCommand.Options.Add(hostOption);

        var modeOption = new Option<AppMode>("--mode", "-m")
        {
            Description = "The remote control mode to use.  Either Attended, Unattended, or Chat.",
            DefaultValueFactory = _ => AppMode.Attended
        };
        rootCommand.Options.Add(modeOption);

        var pipeNameOption = new Option<string>("--pipe-name", "-p")
        {
            Description = "When AppMode is Chat, this is the pipe name used by the named pipes server. " +
                "Use - to read it from standard input. If omitted, the AMARRE_PIPE_NAME environment variable is used."
        };
        pipeNameOption.Validators.Add(result =>
        {
            if (result.GetValue(modeOption) == AppMode.Chat &&
                string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()) &&
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DesktopAccessKey.PipeNameEnvironmentVariable)))
            {
                result.AddError("A pipe name must be specified when AppMode is Chat.");
            }
        });
        rootCommand.Options.Add(pipeNameOption);

        rootCommand.Options.Add(new Option<string>("--session-id", "-s")
        {
            Description = "In Unattended mode, this unique session ID will be assigned to this connection and " +
                "shared with the server.  The connection can then be found in the RemoteControlSessionCache " +
                "using this ID."
        });

        rootCommand.Options.Add(new Option<string>("--access-key", "-a")
        {
            Description = "In Unattended mode, secures access to the connection using the provided key. " +
                "Use - to read it from standard input. If omitted, the AMARRE_ACCESS_KEY environment variable is used."
        });

        rootCommand.Options.Add(new Option<string>("--requester-name", "-r")
        {
            Description = "The name of the technician requesting to connect."
        });

        rootCommand.Options.Add(new Option<string>("--org-name", "-o")
        {
            Description = "The organization name of the technician requesting to connect."
        });

        rootCommand.Options.Add(new Option<bool>("--relaunch")
        {
            Description = "Used to indicate that process is being relaunched from a previous session " +
                "and should notify viewers when it's ready."
        });

        rootCommand.Options.Add(new Option<string>("--viewers")
        {
            Description = "Used with --relaunch.  Should be a comma-separated list of viewers' " +
                "SignalR connection IDs."
        });

        rootCommand.Options.Add(new Option<bool>("--elevate")
        {
            Description = "Must be called from a Windows service.  The process will relaunch " +
                "itself in the console session with elevated rights."
        });

        return rootCommand;
    }
}
