using System;
using System.Collections.Generic;
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
        private class Options
        {
            [Option('t', "table", Required = false, HelpText = "Name of the main table.", Default = "main")] 
            public string TableName { get; set; }
            
            [Option('f', "file", Required = true, HelpText = "Path to the JSON file.")]
            public string File { get; set; }

            [Option("sqlite", HelpText = "SQLite engine", Group = "engine")]
            public bool SQLite { get; set; }
            
            [Option("mysql", HelpText = "MySQL engine", Group = "engine")]
            public bool MySql { get; set; }
            
            [Option("postgres", HelpText = "Postgres engine", Group = "engine")]
            public bool Postgres { get; set; }
        }
        
        public static void Main(string[] args) => Parser.Default.ParseArguments<Options>(args).WithParsed(CommandLineParser);

        private static void CommandLineParser(Options options)
        {
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
                    Console.WriteLine();
                }
                
                if (options.MySql)
                {
                    Console.WriteLine("-- MySQL");
                    Console.WriteLine(Parse((JObject) json, null, options.TableName, Engine.MySql));
                    Console.WriteLine();
                }
                
                if (options.Postgres)
                {
                    Console.WriteLine("-- Postgres");
                    Console.WriteLine(Parse((JObject) json, null, options.TableName, Engine.Postgres));
                    Console.WriteLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Parsing error: {e.Message}");
            }
        }

        private static string Parse(JObject json, string relation, string tableName, Engine engine)
        {
            var newTableName = (relation != null ? relation + "_" : string.Empty) + tableName;
            
            var builder = new StringBuilder($"create table {newTableName} (");

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
                        builder.Append($", {key} integer not null");
                        break;
                    case JTokenType.String:
                        builder.Append($", {key} varchar(255) not null");
                        break;
                    case JTokenType.Boolean:
                        builder.Append($", {key} boolean not null");
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
                                    throw new Exception($"The Null type is not supported in objects and arrays. Set the values explicitly.");
                                default:
                                    throw new Exception($"The {value.Type} type is not supported in arrays");
                            }
                        }
                        
                        break;
                    case JTokenType.Float:
                        builder.Append($", {key} float not null");
                        break;
                    case JTokenType.Null:
                        throw new Exception($"The Null type is not supported in objects and arrays. Set the values explicitly.");
                    default:
                        throw new Exception($"The {value.Type} type is not supported in objects");
                }
            }

            if (relation != null) builder.Append($", foreign key ({relation}_id) references {relation}(id)");

            builder.Append(")");
            if (engine == Engine.MySql) builder.Append($" engine InnoDB");
            builder.Append(";");

            foreach (var query in postAppend)
            {
                builder.Append($"\n{query}");
            }
            
            return builder.ToString();
        }
    }
}
