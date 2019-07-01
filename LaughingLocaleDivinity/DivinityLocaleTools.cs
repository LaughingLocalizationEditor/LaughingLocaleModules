using LaughingLocale.ViewModel.Locale;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using Alphaleonis.Win32.Filesystem;
using System.Threading.Tasks;
using LSLib.LS.Enums;
using LaughingLocale.Divinity.Data;
using LSLib.LS;
using System.Linq;
using System.Globalization;
using LaughingLocale.Data.Locale;

namespace LaughingLocale.Divinity
{
	public static class DivinityLocaleTools
	{
		#region Loading Localization Files

		public static IObservable<LocaleContainer> LoadLocalizationDataAsync(string directoryPath)
		{
			return Observable.Create<LocaleContainer>(
				o => Observable.ToAsync<string, LocaleContainer>(LoadLocalizationData)(directoryPath).Subscribe(o)
			);
			
		}

		public static LocaleContainer LoadLocalizationData(string directoryPath)
		{
			DirectoryInfo directory = new DirectoryInfo(directoryPath);
			var files = FindLocaleFiles(directoryPath);

			LocaleContainer localeContainer = new LocaleContainer()
			{
				Source = directoryPath,
				Name = directory.Name
			};
			foreach(var fpath in files)
			{

			}
			return localeContainer;
		}

		private static IEnumerable<string> FindLocaleFiles(string directoryPath)
		{
			DirectoryEnumerationFilters filters = new DirectoryEnumerationFilters()
			{
				InclusionFilter = f =>
				{
					return f.Extension.Equals("lsb", StringComparison.OrdinalIgnoreCase) == true ||
						f.Extension.Equals("lsj", StringComparison.OrdinalIgnoreCase) == true;
				},
				ErrorFilter = delegate (int errorCode, string errorMessage, string pathProcessed)
				{
					var gotException = errorCode == 5;
					if (gotException)
					{
						Log.Here().Error($"Error reading file at '{pathProcessed}': [{errorCode}]({errorMessage})");
					}
					return gotException;
				},
				RecursionFilter = f =>
				{
					return true;
				}
			};
			return Directory.EnumerateFiles(directoryPath,
				DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.ContinueOnException | DirectoryEnumerationOptions.Recursive, filters);
		}

		public static DivinityLocaleFile LoadResource(string filePath)
		{
			var resourceFormat = ResourceFormat.LSB;
			if (FileExtensionFound(filePath, ".lsj"))
			{
				resourceFormat = ResourceFormat.LSJ;

			}

			var resource = LSLib.LS.ResourceUtils.LoadResource(filePath, resourceFormat);

			DivinityLocaleFile data = new DivinityLocaleFile()
			{
				Resource = resource,
				Format = resourceFormat,
				Source = filePath,
				Name = Path.GetFileName(filePath)
			};
			LoadFromResource(data, resource, resourceFormat);
			return data;
		}

		public static bool LoadFromResource(DivinityLocaleFile fileData, Resource resource, ResourceFormat resourceFormat, bool sort = true)
		{
			try
			{
				if (resourceFormat == ResourceFormat.LSB)
				{
					var rootNode = resource.Regions.First().Value;
					foreach (var entry in rootNode.Children)
					{
						foreach (var node in entry.Value)
						{
							DivinityLocaleEntry localeEntry = LoadFromNode(node, resourceFormat);
							localeEntry.Parent = fileData;
							fileData.Entries.Add(localeEntry);
						}

					}
				}

				if (resourceFormat == ResourceFormat.LSJ || resourceFormat == ResourceFormat.LSX)
				{
					var rootNode = resource.Regions.First().Value;

					var stringNodes = new List<Node>();

					foreach (var nodeList in rootNode.Children)
					{
						var nodes = FindTranslatedStringsInNodeList(nodeList);
						stringNodes.AddRange(nodes);
					}

					foreach (var node in stringNodes)
					{
						DivinityLocaleEntry localeEntry = LoadFromNode(node, resourceFormat);
						localeEntry.Parent = fileData;
						fileData.Entries.Add(localeEntry);
					}
				}

				if (sort)
				{
					fileData.Entries = fileData.Entries.OrderBy(e => e.Key).ToList();
				}

				return true;
			}
			catch (Exception ex)
			{
				Log.Here().Error($"Error loading from resource: {ex.ToString()}");
				return false;
			}
		}

		public static DivinityLocaleEntry LoadFromNode(Node node, ResourceFormat resourceFormat, bool generateNewHandle = false)
		{
			if (resourceFormat == ResourceFormat.LSB)
			{
				NodeAttribute keyAtt = null;
				NodeAttribute contentAtt = null;
				node.Attributes.TryGetValue("UUID", out keyAtt);
				node.Attributes.TryGetValue("Content", out contentAtt);

				DivinityLocaleEntry localeEntry = new DivinityLocaleEntry()
				{
					SourceNode = node,
					KeyAttribute = keyAtt,
					Locked = false
				};

				if (contentAtt == null)
				{
					contentAtt = new NodeAttribute(NodeAttribute.DataType.DT_TranslatedString);
					if (contentAtt.Value is TranslatedString translatedString)
					{
						translatedString.Value = "";
						translatedString.Handle = CreateHandle();
						localeEntry.TranslatedString = translatedString;
					}
					node.Attributes.Add("Content", contentAtt);
				}
				else
				{
					localeEntry.TranslatedString = contentAtt.Value as TranslatedString;
				}

				if (generateNewHandle)
				{
					localeEntry.TranslatedString.Handle = CreateHandle();
				}

				return localeEntry;
			}
			else if (resourceFormat == ResourceFormat.LSJ || resourceFormat == ResourceFormat.LSX)
			{
				DivinityLocaleEntry localeEntry = new DivinityLocaleEntry()
				{
					SourceNode = node,
					Locked = true
				};
				localeEntry.Key = "Dialog Node";

				NodeAttribute contentAtt = null;
				node.Attributes.TryGetValue("TagText", out contentAtt);
				if (contentAtt != null)
				{
					localeEntry.TranslatedString = contentAtt.Value as TranslatedString;
				}
				return localeEntry;
			}
			return null;
		}

		private static List<Node> FindTranslatedStringsInNodeList(KeyValuePair<string, List<LSLib.LS.Node>> nodeList)
		{
			List<Node> nodes = new List<Node>();
			foreach (var node in nodeList.Value)
			{
				var stringNodes = FindTranslatedStringInNode(node);
				nodes.AddRange(stringNodes);
			}
			return nodes;
		}

		private static List<Node> FindTranslatedStringInNode(LSLib.LS.Node node)
		{
			List<Node> nodes = new List<Node>();
			foreach (var att in node.Attributes)
			{
				if (att.Value.Value is TranslatedString translatedString)
				{
					nodes.Add(node);
					break;
				}
			}

			if (node.ChildCount > 0)
			{
				foreach (var c in node.Children)
				{
					var extraNodes = FindTranslatedStringsInNodeList(c);
					nodes.AddRange(extraNodes);
				}
			}

			return nodes;
		}
		#endregion

		#region Saving

		public static int BackupDataFiles(IEnumerable<ILocaleContainer> files, string backupDirectory)
		{
			int successes = 0;
			try
			{
				if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

				List<string> sourceFiles = new List<string>();
				string sysFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace("/", "-");
				string dateFormat = DateTime.Now.ToString(sysFormat + "_HH-mm-ss");

				foreach (var e in files.Where(d => File.Exists(d.Source)))
				{
					sourceFiles.Add(e.Source);
				}

				if (sourceFiles.Count > 0)
				{
					foreach(var f in sourceFiles)
					{
						var outputFile = Path.GetFileNameWithoutExtension(f) + dateFormat + Path.GetExtension(f);
						try
						{
							File.Copy(f, outputFile);
							Log.Here().Activity($"Backed up localization file '{f}' to '{outputFile.ToString()}'");
							successes += 1;
						}
						catch(Exception ex)
						{
							Log.Here().Error($"Error backing up localization file: {ex.ToString()}");
						}
					}
				}
				else
				{
					Log.Here().Activity("Skipping localization backup, as no files were found.");
				}
			}
			catch (Exception ex)
			{
				Log.Here().Error($"Error backing up localization files: {ex.ToString()}");
			}
			return successes;
		}

		public static int SaveDataFiles(IEnumerable<ILocaleContainer> files)
		{
			int success = 0;
			foreach (var f in files.Cast<DivinityLocaleFile>())
			{
				success += SaveDataFile(f);
				f.ChangesUncommitted = false;
			}
			Log.Here().Activity($"Files saved: '{success}'.");
			return success;
		}

		public static int SaveDataFile(DivinityLocaleFile dataFile)
		{
			try
			{
				if (dataFile.Source != null)
				{
					Log.Here().Activity($"Saving '{dataFile.Name}' to '{dataFile.Source}'.");
					LSLib.LS.ResourceUtils.SaveResource(dataFile.Resource, dataFile.Source, dataFile.Format);
					Log.Here().Important($"Saved '{dataFile.Source}'.");
					return 1;
				}
			}
			catch (Exception ex)
			{
				Log.Here().Error($"Error saving localizaton resource: {ex.ToString()}");
			}
			return 0;
		}

		public static string ExportDataAsXML(ILocaleContainer data, bool exportSourceName = true, bool exportKeyName = true)
		{
			//string output = "<contentList>\n{0}</contentList>";
			string output = "";

			var sourcePath = EscapeXml(Path.GetFileName(data.Source));

			foreach (DivinityLocaleEntry e in data.Entries.Where(f => f.Selected).Cast< DivinityLocaleEntry>())
			{
				var sourceStr = "";

				if (exportSourceName)
				{
					sourceStr = $" Source=\"{sourcePath}\"";
				}

				var keyStr = "";

				if (exportKeyName && !String.IsNullOrWhiteSpace(e.Key) && !e.Locked)
				{
					keyStr = $" Key=\"{e.Key}\"";
				}

				string content = EscapeXml(e.Content);

				string addStr = "\t" + "<content contentuid=\"{0}\"{1}{2}>{3}</content>" + Environment.NewLine;
				output += String.Format(addStr, e.Handle, sourceStr, keyStr, content);
			}

			return output;
		}

		private const string defaultLocaleResource = @"<?xml version=""1.0"" encoding=""utf-8""?>
<save>
	<header version=""2"" time=""0"" />
	<version major=""3"" minor=""6"" revision=""4"" build=""0"" />
	<region id=""TranslatedStringKeys"">
		<node id=""root"">
			<children>
				<node id=""TranslatedStringKey"">
					<attribute id=""Content"" value="""" type=""28"" handle=""ls::TranslatedStringRepository::s_HandleUnknown"" />
					<attribute id=""ExtraData"" value="""" type=""23"" />
					<attribute id=""Speaker"" value="""" type=""22"" />
					<attribute id=""Stub"" value=""True"" type=""19"" />
					<attribute id=""UUID"" value=""NewKey"" type=""22"" />
				</node>
			</children>
		</node>
	</region>
</save>";

		public static Resource CreateLocalizationResource()
		{
			try
			{
				using (var stream = new System.IO.MemoryStream())
				{
					var writer = new System.IO.StreamWriter(stream);
					writer.Write(defaultLocaleResource);
					writer.Flush();
					stream.Position = 0;
					Log.Here().Activity("Creating default localization resource.");
					var resource = LSLib.LS.ResourceUtils.LoadResource(stream, ResourceFormat.LSX);
					return resource;
				}
			}
			catch (Exception ex)
			{
				Log.Here().Error($"Error creating new localization resource: {ex.ToString()}");
				return null;
			}
		}
		#endregion

		#region Utilities
		/// <summary>
		/// Handles are a GUID with the dashes replaces with g, and an h prepended to the front.
		/// </summary>
		/// <returns></returns>
		public static string CreateHandle()
		{
			return Guid.NewGuid().ToString().Replace('-', 'g').Insert(0, "h");
		}

		public static DivinityLocaleFile CreateFileData(string destinationPath, string name)
		{
			var resource = CreateLocalizationResource();
			var fileData = new DivinityLocaleFile()
			{
				Resource = resource,
				Format = ResourceFormat.LSB
			};
			LoadFromResource(fileData, resource, ResourceFormat.LSB, true);
			fileData.ChangesUncommitted = true;
			return fileData;
		}

		public static string EscapeXml(string s)
		{
			string toxml = s;
			if (!string.IsNullOrEmpty(toxml))
			{
				// replace literal values with entities
				toxml = toxml.Replace("&", "&amp;");
				toxml = toxml.Replace("'", "&apos;");
				toxml = toxml.Replace("\"", "&quot;");
				toxml = toxml.Replace(">", "&gt;");
				toxml = toxml.Replace("<", "&lt;");
			}
			return toxml;
		}

		public static DivinityLocaleEntry CreateNewLocaleEntry(DivinityLocaleFile fileData, string key = "NewKey", string content = "")
		{
			var rootNode = fileData.Resource.Regions.First().Value;

			var refNode = fileData.Entries.Cast<DivinityLocaleEntry>().Where(f => f.SourceNode != null).FirstOrDefault().SourceNode;
			if (refNode != null)
			{
				var node = new Node();
				node.Parent = rootNode;
				node.Name = refNode.Name;
				//Log.Here().Activity($"Node name: {node.Name}");
				foreach (var kp in refNode.Attributes)
				{
					var att = new NodeAttribute(kp.Value.Type);
					att.Value = new TranslatedString()
					{
						Value = "",
						Handle = CreateHandle()
					};
					node.Attributes.Add(kp.Key, att);
				}

				DivinityLocaleEntry localeEntry = LoadFromNode(node, fileData.Format);
				localeEntry.Key = key == "NewKey" ? key + (fileData.Entries.Count + 1) : key;
				localeEntry.Content = content;
				return localeEntry;
			}
			return null;
		}

		public static bool FileExtensionFound(string fPath, params string[] extensions)
		{
			if (extensions.Length > 1)
			{
				Array.Sort(extensions, StringComparer.OrdinalIgnoreCase);
				int result = Array.BinarySearch(extensions, Path.GetExtension(fPath), StringComparer.OrdinalIgnoreCase);
				//Log.Here().Activity($"Binary search: {fPath} [{string.Join(",", extensions)}] = {result}");
				return result > -1;
			}
			else if (extensions.Length == 1)
			{
				return extensions[0].Equals(Path.GetExtension(fPath), StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public static List<DivinityLocaleFile> ImportFilesAsData(IEnumerable<string> files, string targetDirectory)
		{
			List<DivinityLocaleFile> newFileDataList = new List<DivinityLocaleFile>();
			try
			{
				foreach (var path in files)
				{
					if (FileExtensionFound(path, ".lsb", ".lsj"))
					{
						var fileData = LoadResource(path);
						newFileDataList.Add(fileData);
					}
					else if (FileExtensionFound(path, ".txt", ".tsv", ".csv"))
					{
						char delimiter = '\t';
						if (FileExtensionFound(path, ".csv")) delimiter = ',';

						string line = String.Empty;
						using (var stream = new System.IO.StreamReader(path))
						{
							string sourcePath = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(path), ".lsb");
							string name = Path.GetFileName(path);
							DivinityLocaleFile fileData = CreateFileData(sourcePath, name);

							int lineNum = 0;
							while ((line = stream.ReadLine()) != null)
							{
								lineNum += 1;
								// Skip top line, as it typically describes the columns
								Log.Here().Activity(line);
								if (lineNum == 1 && line.Contains("Key\tContent")) continue;
								var parts = line.Split(delimiter);

								var key = parts.ElementAtOrDefault(0);
								var content = parts.ElementAtOrDefault(1);

								if (key == null) key = "NewKey";
								if (content == null) content = "";

								var entry = CreateNewLocaleEntry(fileData, key, content);
								fileData.Entries.Add(entry);
							}

							//Remove the empty default new key
							if (fileData.Entries.Count > 1)
							{
								fileData.Entries.Remove(fileData.Entries.First());
							}

							newFileDataList.Add(fileData);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Here().Error("Error importing files to the localization editor: " + ex.ToString());
			}
			return newFileDataList;
		}

		public static List<ILocaleData> ImportFilesAsEntries(IEnumerable<string> files, DivinityLocaleFile fileData)
		{
			List<ILocaleData> newEntryList = new List<ILocaleData>();
			try
			{
				foreach (var path in files)
				{
					Log.Here().Activity($"Checking file '{path}'");

					if (FileExtensionFound(path, ".lsb", ".lsj"))
					{
						Log.Here().Activity($"Creating entries from resource.");
						var tempData = LoadResource(path);
						newEntryList.AddRange(tempData.Entries);
					}
					else if (FileExtensionFound(path, ".txt", ".tsv", ".csv"))
					{
						Log.Here().Activity($"Creating entries from delimited text file.");
						char delimiter = '\t';
						if (FileExtensionFound(path, ".csv")) delimiter = ',';

						string line = String.Empty;
						using (var stream = new System.IO.StreamReader(path))
						{
							int lineNum = 0;
							while ((line = stream.ReadLine()) != null)
							{
								lineNum += 1;
								// Skip top line, as it typically describes the columns
								if (lineNum == 1 && line.Contains("Key\tContent")) continue;
								var parts = line.Split(delimiter);

								var key = parts.ElementAtOrDefault(0);
								var content = parts.ElementAtOrDefault(1);

								if (key == null) key = "NewKey";
								if (content == null) content = "";

								var entry = CreateNewLocaleEntry(fileData, key, content);
								newEntryList.Add(entry);
							}
							stream.Close();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Here().Error("Error importing files as entries to the localization editor: " + ex.ToString());
			}
			return newEntryList;
		}
		#endregion
	}
}
