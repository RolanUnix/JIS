# JIS
Translator of the JSON file structure to the SQL structure.

## Dependences
Libraries are required to run the program:

- Newtonsoft.Json `12.0.3`

- CommandLineParser `2.7.82`

## Quick start
If you have .NET Core version `2.2` or higher installed, run the following commands on Linux:

```bash
$ dotnet JIS.dll --help
```

To run on Windows, use powershell or cmd and run .exe file:

```cmd
JIS.exe --help
```

## Options reference:
`-t or --table` is the name of the main table.

`-f or --file` is the path to the JSON file.

`--character` is character table.

`--collate` is collate table.

`--insert` this option generates data insertion.

`--sqlite` this option indicates that the structure for the SQLite engine will be generated.

`--mysql` this option indicates that the structure for the MySQL engine will be generated.

`--postgres` this option indicates that the structure for the Postgres engine will be generated.

## Example

You need to generate the database structure from the JSON file test.json and maintain the structure of the Postgres DBMS to test.sql:

```bash
$ dotnet JIS.dll -f test.json --postgres > test.sql
```
