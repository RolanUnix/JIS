using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Input;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JIS
{
    internal static class Program
    {
        private static Options _options;
        
        private class Options
        {
            [Option('t', "table", Required = false, HelpText = "Name of the main table.", Default = "main")] 
            public string TableName { get; set; }
            
            [Option('f', "file", Required = true, HelpText = "Path to the JSON file.")]
            public string File { get; set; }

            [Option("character", Required = false, HelpText = "Character table.", Default = "utf8")]
            public string Character { get; set; }

            [Option("collate", Required = false, HelpText = "Collate table.", Default = "utf8_general_ci")]
            public string Collate { get; set; }
            
            [Option("insert", Required = false, HelpText = "Insertion data.", Default = false)]
            public bool Insert { get; set; }
            
            [Option("sqlite", HelpText = "SQLite engine", Group = "DBMS")]
            public bool SQLite { get; set; }
            
            [Option("mysql", HelpText = "MySQL engine", Group = "DBMS")]
            public bool MySql { get; set; }
            
            [Option("postgres", HelpText = "Postgres engine", Group = "DBMS")]
            public bool Postgres { get; set; }
        }
        
        public static void Main(string[] args) => Parser.Default.ParseArguments<Options>(args).WithParsed(CommandLineParser);

        private static void CommandLineParser(Options options)
        {
            _options = options;
            JToken json;
            
            try
            {
                json = JToken.Parse(File.ReadAllText(options.File));
            }
            catch (Exception e)
            {
                Console.WriteLine($"The JSON file could not be parsed.\nError: {e.Message}");
                return;
            }

            try
            {
                if (json.Type != JTokenType.Object)
                    throw new Exception("The input JSON file must be an object type");

                if (options.SQLite)
                {
                    Console.WriteLine("-- SQlite");
                    Console.WriteLine(Parse((JObject) json, null, options.TableName, Engine.SQLite));
                    
                    if (options.Insert)
                    {
                        Console.WriteLine("-- Insert");
                        Console.WriteLine(Import((JObject) json, null, options.TableName, Engine.SQLite));    
                    }
                    
                    Console.WriteLine();
                }
                
                if (options.MySql)
                {
                    Console.WriteLine("-- MySQL");
                    Console.WriteLine(Parse((JObject) json, null, options.TableName, Engine.MySql));
                    
                    if (options.Insert)
                    {
                        Console.WriteLine("-- Insert");
                        Console.WriteLine(Import((JObject) json, null, options.TableName, Engine.MySql));   
                        
                    }
                    Console.WriteLine();
                }
                
                if (options.Postgres)
                {
                    Console.WriteLine("-- Postgres");
                    Console.WriteLine(Parse((JObject) json, null, options.TableName, Engine.Postgres));
                    
                    if (options.Insert)
                    {
                        Console.WriteLine("-- Insert");
                        Console.WriteLine(Import((JObject) json, null, options.TableName, Engine.Postgres));    
                    }
                    
                    Console.WriteLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Parsing error: {e}");
            }
        }

        private static string Parse(JObject json, string relation, string tableName, Engine engine)
        {
            var newTableName = (relation != null ? relation + "_" : string.Empty) + tableName;
            
            var builder = new StringBuilder($"create table if not exists {newTableName} (");

            switch (engine)
            {
                case Engine.SQLite:
                    builder.Append("id integer primary key autoincrement");
                    break;
                case Engine.MySql:
                    builder.Append("id integer primary key auto_increment");
                    break;
                case Engine.Postgres:
                    builder.Append("id serial primary key");
                    break;
            }

            if (relation != null) builder.Append($", {relation}_id integer not null");

            var postAppend = new List<string>();
            
            foreach (var (key, value) in json)
            {
                switch (value.Type)
                {
                    case JTokenType.Integer:
                        builder.Append($", {key} integer null default null");
                        break;
                    case JTokenType.String:
                        builder.Append($", {key} {(engine == Engine.Postgres ? "varchar" : "text")}(65535) null default null");
                        break;
                    case JTokenType.Boolean:
                        builder.Append($", {key} boolean null default null");
                        break;
                    case JTokenType.Object:
                        postAppend.Add(Parse(value.ToObject<JObject>(), newTableName, key, engine));
                        break;
                    case JTokenType.Array:
                        var array = value.ToObject<JArray>();
                        
                        if (array.Count != 0)
                        {
                            switch (array[0].Type)
                            {
                                case JTokenType.Integer:
                                    postAppend.Add(Parse(new JObject()
                                    {
                                        {"value", 0}
                                    }, newTableName, key, engine));
                                    break;
                                case JTokenType.String:
                                    postAppend.Add(Parse(new JObject()
                                    {
                                        {"value", string.Empty}
                                    }, newTableName, key, engine));
                                    break;
                                case JTokenType.Boolean:
                                    postAppend.Add(Parse(new JObject()
                                    {
                                        {"value", false}
                                    }, newTableName, key, engine));
                                    break;
                                case JTokenType.Float:
                                    postAppend.Add(Parse(new JObject()
                                    {
                                        {"value", 0.0f}
                                    }, newTableName, key, engine));
                                    break;
                                case JTokenType.Object:
                                    postAppend.Add(Parse(array[0].ToObject<JObject>(), newTableName, key, engine));
                                    break;
                                case JTokenType.Null:
                                    continue;
                                default:
                                    throw new Exception($"The {value.Type} type is not supported in arrays");
                            }
                        }
                        
                        break;
                    case JTokenType.Float:
                        builder.Append($", {key} float null default null");
                        break;
                    case JTokenType.Date:
                        builder.Append($", {key} {(engine == Engine.Postgres ? "date" : "datetime")} null default null");
                        break;
                    case JTokenType.Null:
                        builder.Append($", {key} {(engine == Engine.Postgres ? "varchar" : "text")}(65535) null default null");
                        break;
                    default:
                        Console.WriteLine(key);
                        throw new Exception($"The {value.Type} type is not supported in objects");
                }
            }

            if (relation != null) builder.Append($", foreign key ({relation}_id) references {relation}(id)");

            builder.Append(")");
            
            if (engine == Engine.MySql)
            {
                builder.Append($" engine InnoDB");
                builder.Append($" character set {_options.Character} collate {_options.Collate}");
            }

            builder.Append(";");
            
            foreach (var query in postAppend)
            {
                builder.Append($"\n{query}");
            }
            
            return builder.ToString();
        }

        private static string Import(JObject json, string relation, string tableName, Engine engine)
        {
            var newTableName = (relation != null ? relation + "_" : string.Empty) + tableName;
            var builder = new StringBuilder($"insert into {newTableName} ");
            var postAppend = new List<string>();
            var structureParameters = new List<string>();

            if (relation != null) structureParameters.Add($"{relation}_id");
            
            foreach (var (key, value) in json)
            {
                switch (value.Type)
                {
                    case JTokenType.Object:
                        continue;
                    case JTokenType.Array:
                        continue;
                    case JTokenType.Null:
                        continue;
                    default:
                        structureParameters.Add(key);
                        break;
                }
            }

            builder.Append($"({string.Join(", ", structureParameters)}) values ");
            var valuesParameters = new List<string>();
            
            if (relation != null) valuesParameters.Add($"(select MAX(id) from {relation})");
            
            foreach (var (key, value) in json)
            {
                switch (value.Type)
                {
                    case JTokenType.Integer:
                        valuesParameters.Add(value.ToObject<int>().ToString());
                        break;
                    case JTokenType.String:
                        valuesParameters.Add($"'{value.ToObject<string>()}'");
                        break;
                    case JTokenType.Boolean:
                        valuesParameters.Add(value.ToObject<bool>() ? (engine != Engine.MySql ? "true": "1") : (engine != Engine.MySql ? "false": "0"));
                        break;
                    case JTokenType.Float:
                        valuesParameters.Add(value.ToObject<double>().ToString(CultureInfo.InvariantCulture));
                        break;
                    case JTokenType.Date:
                        valuesParameters.Add($"'{DateTime.Parse(value.ToObject<string>()).ToString("yyyy-MM-dd hh:mm:ss")}'");
                        break;
                    case JTokenType.Object:
                        postAppend.Add(Import(value.ToObject<JObject>(), newTableName, key, engine));
                        break;
                    case JTokenType.Array:
                        postAppend.Add(string.Join('\n', ImportArray(value.ToObject<JArray>(), newTableName, key, engine)));
                        break;
                    case JTokenType.Null:
                        continue;
                    default:
                        Console.WriteLine(key);
                        throw new Exception($"The {value.Type} type is not supported in objects");
                }
            }

            builder.Append($"({string.Join(", ", valuesParameters)});");

            foreach (var query in postAppend)
            {
                builder.Append($"\n{query}");
            }
            
            return builder.ToString();
        }

        private static IEnumerable<string> ImportArray(JArray json, string relation, string tableName, Engine engine)
        {
            var newTableName = $"{relation}_{tableName}";
            var list = new List<string>();
            
            foreach (var value in json)
            {
                switch (value.Type)
                {
                    case JTokenType.Integer:
                        list.Add($"insert into {newTableName} ({relation}_id, value) values ((select MAX(id) from {relation}), {value.ToObject<int>()});");
                        break;
                    case JTokenType.String:
                        list.Add($"insert into {newTableName} ({relation}_id, value) values ((select MAX(id) from {relation}), '{value.ToObject<string>()}');");
                        break;
                    case JTokenType.Boolean:
                        list.Add($"insert into {newTableName} ({relation}_id, value) values ((select MAX(id) from {relation}), {(value.ToObject<bool>() ? (engine != Engine.MySql ? "true": "1") : (engine != Engine.MySql ? "false": "0"))});");
                        break;
                    case JTokenType.Float:
                        list.Add($"insert into {newTableName} ({relation}_id, value) values ((select MAX(id) from {relation}), {value.ToObject<float>().ToString(CultureInfo.InvariantCulture)});");
                        break;
                    case JTokenType.Object:
                        list.Add(Import(value.ToObject<JObject>(), relation, tableName, engine));
                        break;
                }
            }

            return list;
        }
    }
}
