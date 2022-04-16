// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const lightCodeTheme = require("prism-react-renderer/themes/github");
const darkCodeTheme = require("prism-react-renderer/themes/dracula");

/** @type {import('@docusaurus/types').Config} */
const config = {
    title: "CF-MC-Server",
    tagline: "Launching modpack servers for Minecraft",
    url: "https://curseforgecommunity.github.io",
    baseUrl: "/cf-mc-server/",
    onBrokenLinks: "throw",
    onBrokenMarkdownLinks: "warn",
    organizationName: "CurseForgeCommunity", // Usually your GitHub org/user name.
    projectName: "cf-mc-server", // Usually your repo name.
    deploymentBranch: "gh-pages",
    trailingSlash: false,
    presets: [
        [
            "classic",
            /** @type {import('@docusaurus/preset-classic').Options} */
            ({
                docs: {
                    sidebarPath: require.resolve("./sidebars.js"),
                    editUrl:
                        "https://github.com/CurseForgeCommunity/cf-mc-server/docs/",
                },
                blog: {
                    showReadingTime: true,
                    editUrl:
                        "https://github.com/CurseForgeCommunity/cf-mc-server/docs/",
                },
                theme: {
                    customCss: require.resolve("./src/css/custom.css"),
                },
            }),
        ],
    ],

    themeConfig:
        /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
        ({
            navbar: {
                title: "CF-MC-Server",
                items: [
                    {
                        type: "doc",
                        docId: "how-to-run-cf-mc-server",
                        position: "left",
                        label: "How to run CF-MC-Server",
                    },
                    { to: "/blog", label: "Blog", position: "left" },
                    {
                        href: "https://github.com/CurseForgeCommunity/cf-mc-server",
                        label: "GitHub",
                        position: "right",
                    },
                ],
            },
            footer: {
                style: "dark",
                links: [
                    {
                        title: "Community",
                        items: [
                            {
                                label: "GitHub Discussions",
                                href: "https://github.com/CurseForgeCommunity/cf-mc-server/discussions",
                            },
                            {
                                label: "Twitter",
                                href: "https://twitter.com/NoLifeKing85",
                            },
                        ],
                    },
                    {
                        title: "More",
                        items: [
                            {
                                label: "Blog",
                                to: "/blog",
                            },
                            {
                                label: "GitHub",
                                href: "https://github.com/CurseForgeCommunity/cf-mc-server",
                            },
                        ],
                    },
                ],
                copyright: `Copyright © ${new Date().getFullYear()} CF-MC-Server Built with Docusaurus.`,
            },
            prism: {
                theme: lightCodeTheme,
                darkTheme: darkCodeTheme,
            },
            colorMode: {
                defaultMode: "dark",
                respectPrefersColorScheme: true,
            },
        }),
};

module.exports = config;
