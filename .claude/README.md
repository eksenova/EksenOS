# EksenOS Claude Code Plugin Marketplace

This directory is a Claude Code plugin marketplace. The marketplace manifest lives at
`.claude-plugin/marketplace.json` and the single plugin it publishes (`eksenos`) is the
marketplace root itself.

## Layout

```
.claude/
├── .claude-plugin/
│   ├── marketplace.json     # marketplace catalog (name, owner, plugins)
│   └── plugin.json          # the eksenos plugin manifest
├── conventions/
│   └── running-example.md   # the e-commerce domain every skill must use
├── skills/                  # one skill per migration phase (added later)
└── README.md
```

## Install in a consumer repo

Add the marketplace, then install the plugin:

```shell
/plugin marketplace add <this-repo-clone-url>
/plugin install eksenos@eksenos-marketplace
```

To register the marketplace declaratively for a whole team, add it to the consumer repo's
`.claude/settings.json`. While EksenOS is consumed as a git submodule, point at the local
directory:

```jsonc
{
  "extraKnownMarketplaces": {
    "eksenos-marketplace": {
      "source": {
        "source": "directory",
        "path": "src/eksenos/.claude"
      }
    }
  },
  "enabledPlugins": {
    "eksenos@eksenos-marketplace": true
  }
}
```

After EksenOS publishes a tagged release, switch the source to `git`/`github` with a pinned
`ref`.

The running domain example used across every skill is locked in
`./conventions/running-example.md`. Each migration phase contributes one skill under
`./skills/`.
