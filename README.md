# Simple Migrator

## Installation
This is intended to be installed as a [dotnet tool](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install).

## Usage
``` bash
migrate postgres up|down --connection-string <postgres connection string> --folder <folder of migration files> [--to <migration name to stop at>]
```

Migration files are executed in order, so a date-related naming convention is recommended, but not required. See the [example migrations folder](example/).

## Contribution
While you have every right to fork this repo and run away with it, I would really appreciate comments and pull requests if you have ideas!
