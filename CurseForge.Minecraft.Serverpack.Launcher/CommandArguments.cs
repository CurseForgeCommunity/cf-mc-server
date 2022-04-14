using System.CommandLine;
using System.CommandLine.Invocation;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static RootCommand SetupCommand()
		{
			RootCommand command = new(
				description: @"Installs a CurseForge Modpack as a server as far as it can.

Example:
  cf-mc-server 477455 3295539 ""c:\mc-server""
  -- Installs the modpack ""Too Many Projects"", into the ""mc-server"" folder in the C-drive"
			);

			SetupSubcommand(command); ;
			SetupArguments(command);
			SetupOptions(command);

			command.Handler = CommandHandler.Create<int, int, string, string, bool>(async (projectid, fileid, serverPath, javaArgs, startServer) => {
				return await InstallServer(projectid, fileid, serverPath, javaArgs, startServer);
			});

			return command;
		}

		private static void SetupSubcommand(RootCommand command)
		{
			var interactive = new Command("interactive",
				description: @"The interactive mode lets you search and select what modpack you want to use.
This will search for modpacks from CurseForge.");

			interactive.Handler = CommandHandler.Create(async () => {
				return await InteractiveInstallation();
			});

			command.Add(interactive);
		}

		private static void SetupOptions(RootCommand command)
		{
			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--projectid",
						"-pid"
					},
					description: "Sets the project id / modpack id to use",
					argumentType: typeof(long)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--fileid",
						"-fid"
					},
					description: "Sets the file id to use",
					argumentType: typeof(long)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--server-path",
						"-sp"
					},
					description: "Sets the server path, where to install the modpack server",
					argumentType: typeof(string)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--java-args",
						"-ja"
					},
					description: "Sets the java arguments to be used when launching the Minecraft server",
					argumentType: typeof(string)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[]
					{
						"--start-server",
						"-start"
					},
					description: "Makes the server start when it's done installing the server and modpack",
					argumentType: typeof(bool)
				)
			);
		}

		private static void SetupArguments(RootCommand command)
		{
			command.AddArgument(new("projectid")
			{
				ArgumentType = typeof(int),
				Arity = ArgumentArity.ZeroOrOne,
				Description = "Sets the project id / modpack id to use",
			});

			command.AddArgument(new("fileid")
			{
				ArgumentType = typeof(int),
				Description = "Sets the file id to use"
			});

			command.AddArgument(new("server-path")
			{
				ArgumentType = typeof(string),
				Description = "Sets the server path, where to install the modpack server"
			});

			command.AddArgument(new("java-arguments")
			{
				ArgumentType = typeof(string),
				Arity = ArgumentArity.ZeroOrOne,
				Description = "Sets the java arguments to be used when launching the Minecraft server"
			});
		}
	}
}
