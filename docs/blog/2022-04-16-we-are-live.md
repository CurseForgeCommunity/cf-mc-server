---
slug: we-are-live
title: We are live!
authors: nolifeking85
tags: [minecraft, curseforge, modpacks]
---

A tool to install modpack servers, almost without having to do anything at all?

I mean, why not?

<!--truncate-->

I've been sitting in the [CurseForge Discord](https://discord.com/invite/curseforge) for a while,
and one thing that I've seen from time to time, is that people have troubles of setting up a server for the modpack they want to run (or they simple don't want to pay a server host, to solve that issue).

So, I had the stupid idea of building a tool, that could potentially solve big parts of the problems with installing servers for modpacks, as it require at least a little bit of technical knowhow.

I started building this tool, tested it a bit with my own personal modpack (really small, simple and unconfigured),
got it to work, but I still felt that it wasn't enough, since it required you to know a lot of things in beforehand.

Example on how you would install my modpack from command line, when I first started out

```shell
C:\minecraft-server\> cf-mc-server 477455 3295539 "c:\minecraft-server"
```

Simple, right? So let me explain what happens here..

`477455` is the project ID for my modpack ([Too Many Projects](https://www.curseforge.com/minecraft/modpacks/too-many-projects)), and `3295539` is the file ID, for the latest (only) version of my modpack.

.. and I hope you can figure out what `"c:\minecraft-server"` would do..

Now, I felt that, not everyone will be able to figure out how to get these IDs by themselves, so I wanted to make it easier to install things.

So I made an interactive installer as well, where you can search modpacks and select what version you want to install.

Here's an old video on how the installer looked while I was developing it

<video src="https://itssimple.se/share/u/2022/03/22/0650-0a19.mp4" lazy controls style={{maxWidth: "100%"}}></video>

Yeah, I know. It ain't that pretty, but it sure as hell makes installing a server easier.

:::note Things to consider

This won't work 100% without hitches, since I don't filter away any mods from the modpacks, so if there are badly written client-only mods, they might break the server.

:::
