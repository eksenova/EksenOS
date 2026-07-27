# EksenOS Claude Code Plugin Marketplace

This directory is a Claude Code plugin marketplace. The marketplace manifest lives at
`.claude-plugin/marketplace.json` and the single plugin it publishes (`eksenos`) is the
marketplace root itself.

## Layout

```
.claude-plugin/
├── marketplace.json     # marketplace catalog (name, owner, plugins)
├── plugin.json          # the eksenos plugin manifest
├── skills/              # one skill per migration phase (added later)
└── README.md
```

## Install in a consumer repo

Add the marketplace, then install the plugin:

```shell
/plugin marketplace add eksenova/eksenos
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
        "source": "github",
        "repo": "eksenova/eksenos"
      }
    }
  },
  "enabledPlugins": {
    "eksenos@eksenos-marketplace": true
  }
}
```
