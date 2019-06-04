﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NoPipeline {
	/*
	 * Item object represents a rule in the config file or section of MGCB file
	 * Parameters:
	 *   Param:List of strings - contains all item's configuration parameters to generate MGCB file
	 *   Name:String - file name
	 *   Recursive:Bool - True=search files recursively
	 *   Watch:List of strings - contains masks of files
	 * Methods:
	 *   ToString() - generate recod of the item to store in MGCB file
	 *   Add() - add a new value of Item
	 */
	public class Item {
		public StringBuilder Param { get; set; }
		public string Name { get; set; }
		public bool Recursive { get; set; }
		public List<string> Watch { get; set; }

		public Item() {
			Param = new StringBuilder();
			Recursive = false;
			Watch = new List<string>();
		}

		public override string ToString() {
			return $"#begin {Name.Replace('\\', '/')}" + System.Environment.NewLine + Param.ToString();
		}

		public void Add(string param, JToken value) {
			switch (param) {
				case "path":
					Name = value.ToString().Replace('\\', '/');
					break;
				case "recursive":
					Recursive = value.ToString() == "True";
					break;
				case "action":
					Param.Append($"/{value.ToString()}:{Name.Replace('\\', '/')}{System.Environment.NewLine}");
					break;
				case "watch":
					Watch = value.ToObject<List<string>>();
					break;
				default:
					Param.Append($"/{param}:{value.ToString().Replace('\\', '/')}{System.Environment.NewLine}");
					break;
			}
		}
	}

	/*
	 * MGCB class - represents the mgcb configuration file
	 * Input:
	 *   mgcb file name
	 * Parameters:
	 *   Header:List of strings - contains all global configuration parameters to generate MGCB file
	 *   Items:List of Item - contains list of Item objects
	 *   CfgName:String - Config file name
	 *   CfgPath:String - Config file path
	 * Methods:
	 *   MGCB(conf) - read mgcb config file and return MGCB object
	 *   Add(Item) - Add new Item
	 *   ContentCheck() - Check configuration. Find all files related to the Item and check the modified date of each file.
	 *	                  if file was modified - update the item file modified time to current(if applicable)
	 *	 Save() - store the MGCB object into mgcb congiguration file
	 */
	public class MGCB {
		public StringBuilder Header { get; set; }
		public Dictionary<string, Item> Items { get; set; }
		public string CfgName { get; set; }
		public string CfgPath { get; set; }

		public MGCB(JObject conf) {   // Read mgcb config file
			string root = conf["root"].ToString().TrimEnd('/', '\\');
			string name = conf["root"].ToString().TrimEnd('/', '\\') + "/" + conf["path"].ToString();  // path to Content.mgcb
			if (!File.Exists(name)) {
				throw new Exception($"{name} file not found!");
			}
			CfgName = name;
			CfgPath = Path.GetDirectoryName(name);
			string line;
			bool isItemSection = false;
			Item it = new Item();
			Header = new StringBuilder();
			Items = new Dictionary<string, Item>();

			using (StreamReader file = new StreamReader(name)) {
				while ((line = file.ReadLine()) != null) {
					if (!isItemSection) {
						if (line.StartsWith("#begin")) {
							isItemSection = true;   // found first begin - stop collecting header
						} else {
							Header.AppendLine(line);
						}
					}
					if (isItemSection) {
						if (line.StartsWith("#begin")) {
							it = new Item {
								Name = line.Substring(7)
							};
							Items.Add(it.Name, it); // add to the dictionary
						} else {
							it.Param.AppendLine(line);
						}
					}
				}
			}

		}

		public void Add(Item it) {
			Items.Add(it.Name, it);
		}

		public void ContentCheck() {   // check all items exist
			var ItemsCheck = new Dictionary<string, Item>();

			foreach (Item it in Items.Values) {
				if (File.Exists(CfgPath + "/" + it.Name)) {  // not exists - not include to Items
					DateTime lastModified = File.GetLastWriteTime(CfgPath + "/" + it.Name);
					// check if "watch" present
					try {
						foreach (var file2check in it.Watch) {
							var fileName = Path.GetFileName(file2check);
							var filePath = Path.GetDirectoryName(file2check);
							var files = Directory.GetFiles($"{CfgPath}/{filePath}", fileName, SearchOption.AllDirectories);
							foreach (var f in files) {
								DateTime f_lastModified = File.GetLastWriteTime(f);
								if (lastModified < f_lastModified) {
									// change datetime required
									File.SetLastWriteTime(CfgPath + "/" + it.Name, DateTime.Now);
									break;
								}
							}
						}
					} catch {
						// Update datetime in any error
						File.SetLastWriteTime(CfgPath + "/" + it.Name, DateTime.Now);
					}
					ItemsCheck.Add(it.Name, it);
				}
			}
			Items = ItemsCheck;
		}

		public void Save() { // save config file
			using (var file = new System.IO.StreamWriter(CfgName + ".new")) {
				// header
				file.Write(Header.ToString());
				// items
				foreach (Item it in Items.Values) {
					file.Write(it.ToString());
				}

			}

		}

	}
}
