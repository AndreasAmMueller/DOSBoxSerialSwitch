using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;
using Unclassified.FieldLog;

namespace DOSBoxSerialSwitch
{
	/// <summary>
	/// Interaktionslogik für MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		internal string filePath;
		internal string configFile;
		internal Dictionary<string, object> config;
		internal string[] serialPorts;

		public MainWindow()
		{
			InitializeComponent();
			FL.AcceptLogFileBasePath();

			string[] files = null;
			try
			{
				string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				files = Directory.GetFiles(Path.Combine(appData, "DOSBox"), "dosbox-*.conf", SearchOption.AllDirectories);

				if (files.Length == 0)
				{
					FL.Error("No files found", Path.Combine(appData, "DOSBox"));
					MessageBox.Show("No DOSBox config found.\nClosing application.", "No Config Found", MessageBoxButton.OK);
					Close();
				}
				filePath = files[0];
			}
			catch (Exception ex)
			{
				FL.Error(ex, "Searching DOSBox config");
			}

			try
			{
				using (StreamReader sr = new StreamReader(filePath))
				{
					configFile = sr.ReadToEnd();
				}
			}
			catch (Exception ex)
			{
				FL.Error(ex, $"Reading the config file {filePath}");
			}

			try
			{
				ReadDosBoxFile();
				ReadSerialPorts();

				FillDropDowns();
			}
			catch (Exception)
			{ /* QUIET!! */ }
		}



		private string GetParentDirectory(string path, int up = 1)
		{
			for (int i = 0; i < up; i++)
			{
				path = Directory.GetParent(path).ToString();
			}

			return path;
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void ApplyButton_Click(object sender, RoutedEventArgs e)
		{
			if (DosBoxPorts.SelectedItem == null
				|| SerialPorts.SelectedItem == null)
			{
				MessageBox.Show("Both have to be set", "Port missing", MessageBoxButton.OK);
				return;
			}

			string dosbox = DosBoxPorts.SelectedItem.ToString();
			string serial = SerialPorts.SelectedItem.ToString();

			string[] fileLines = configFile.Split('\n');
			for (int i = 0; i < fileLines.Length; i++)
			{
				string line = fileLines[i];
				if (line.StartsWith("serial"))
				{
					if (line.Contains(serial))
					{
						string[] split = line.Split('=');
						fileLines[i] = $"{split[0]}=disabled{(line.EndsWith("\r") ? "\r" : "")}";
					}

					if (line.StartsWith(dosbox))
					{
						fileLines[i] = $"{dosbox}=directserial realport:{serial}{(line.EndsWith("\r") ? "\r" : "")}";
					}
				}
			}
			configFile = string.Join("\n", fileLines);

			try
			{
				File.WriteAllText(filePath, configFile);
				MessageBox.Show("New settings successful written", "Success", MessageBoxButton.OK);
			}
			catch (Exception ex)
			{
				FL.Error(ex, "Writing config back");
				MessageBox.Show($"File\n{filePath}\nCould not be written", "Write Error", MessageBoxButton.OK);
			}
		}

		private void ReadDosBoxFile()
		{
			string[] fileLines = configFile.Split('\n').Where(l => !(l.StartsWith("#") || string.IsNullOrWhiteSpace(l.Trim()))).ToArray();

			for (int i = 0; i < fileLines.Length; i++)
				fileLines[i] = fileLines[i].Trim();

			string text = string.Join("\n", fileLines);

			Dictionary<string, string> sectionSplit = Regex.Matches(text, @"\[([\w]*)\]\n([^\[^\#]*)")
				.Cast<Match>()
				.ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);

			config = new Dictionary<string, object>(sectionSplit.Count);
			foreach (var section in sectionSplit)
			{
				string[] lines = section.Value.Split('\n');
				if (section.Value.Contains("="))
				{
					Dictionary<string, string> props = Regex.Matches(section.Value, @"(\w*)=(.*)")
						.Cast<Match>()
						.ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
					config.Add(section.Key, props);
				}
				else
				{
					config.Add(section.Key, lines);
				}
			}
		}

		private void ReadSerialPorts()
		{
			serialPorts = SerialPort.GetPortNames();
		}

		private void FillDropDowns()
		{
			DosBoxPorts.IsEnabled = false;
			SerialPorts.IsEnabled = false;

			SerialPorts.Items.Clear();
			SerialPorts.Items.AddRange(serialPorts);
			SerialPorts.IsEnabled = SerialPorts.Items.Count > 0;

			object outO;
			if (config.TryGetValue("serial", out outO))
			{
				var serialPorts = outO as Dictionary<string, string>;
				if (serialPorts != null)
				{
					DosBoxPorts.Items.Clear();
					foreach (var kvp in serialPorts)
					{
						DosBoxPorts.Items.Add(kvp.Key);

						if (kvp.Value.Contains("realport"))
						{
							string serial = kvp.Value.Substring(kvp.Value.IndexOf(":") + 1);
							SerialPorts.Text = serial;
							DosBoxPorts.Text = kvp.Key;
						}
					}

					DosBoxPorts.IsEnabled = DosBoxPorts.Items.Count > 0;
				}
			}

			ApplyButton.IsEnabled = SerialPorts.IsEnabled && DosBoxPorts.IsEnabled;
		}
	}
}
