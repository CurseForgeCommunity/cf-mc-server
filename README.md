# Minecraft Modpack Server installer

A small CLI that allows you to install and run modpacks from CurseForge, as servers!

```plain
cf-mc-server
  Installs a CurseForge Modpack as a server as far as it can.

  Example:
    cf-mc-server 477455 3295539 "c:\mc-server"
    -- Installs the modpack "Too Many Projects", into the "mc-server" folder in the C-drive

Usage:
  cf-mc-server [options] [<projectid> <fileid> <server-path> [<java-arguments>]] [command]

Arguments:
  <projectid>       Sets the project id / modpack id to use
  <fileid>          Sets the file id to use
  <server-path>     Sets the server path, where to install the modpack server
  <java-arguments>  Sets the java arguments to be used when launching the Minecraft server

Options:
  -pid, --projectid <projectid>     Sets the project id / modpack id to use
  -fid, --fileid <fileid>           Sets the file id to use
  -sp, --server-path <server-path>  Sets the server path, where to install the modpack server
  -ja, --java-args <java-args>      Sets the java arguments to be used when launching the Minecraft server
  -start, --start-server            Makes the server start when it's done installing the server and modpack
  --version                         Show version information
  -?, -h, --help                    Show help and usage information

Commands:
  interactive  The interactive mode lets you search and select what modpack you want to use.
               This will search for modpacks from CurseForge.
```

Works on Linux and Windows for now.

Example commands:

```plain
cf-mc-server interactive
```

Launches the interactive mode, where you can search for modpacks, select where to install the server and so on.

```plain
cf-mc-server 477455 3295539 "c:\mc-server"
```

Installs the modpack "Too Many Projects", into the "mc-server" folder in the C-drive