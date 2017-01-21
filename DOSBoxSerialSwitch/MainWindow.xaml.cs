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
using Microsoft.Win32;
using Unclassified.FieldLog;

namespace DOSBoxSerialSwitch
{
	/// <summary>
	/// Interaktionslogik für MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string configFilePath;
		private string configFile;
		private Dictionary<string, string[]> configSections;
		private Dictionary<string, string> mapping;

		public MainWindow()
		{
			InitializeComponent();
			FL.AcceptLogFileBasePath();
			mapping = new Dictionary<string, string>();

			TryGetConfigFile();
			if (configFilePath == null)
			{
				MessageBox.Show("No config file found.\nPlease search manually for it.", "Config file not found", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			else
			{
				LoadConfiguration();
			}
		}

		private void BrowseConfigFileButton_Click(object sender, RoutedEventArgs args)
		{
			string path;
			if (configFilePath == null)
			{
				path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			}
			else
			{
				path = Path.GetDirectoryName(configFilePath);
			}

			OpenFileDialog dialog = new OpenFileDialog
			{
				InitialDirectory = path,
				CheckFileExists = true,
				CheckPathExists = true,
				DefaultExt = ".conf",
				FileName = "dosbox.conf",
				Title = "Open Config File",
				RestoreDirectory = true
			};

			if (dialog.ShowDialog() == true)
			{
				configFilePath = dialog.FileName;
				LoadConfiguration();
			}
		}

		private void ApplyButton_Click(object sender, RoutedEventArgs args)
		{
			if (SerialPortsDosBox.SelectedItem == null ||
				string.IsNullOrEmpty(SerialPortsSystem.Text.Trim()))
			{
				MessageBox.Show("Port can not be mapped.\nThere is an information missing.", "Mapping not possible", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}

			string dosbox = SerialPortsDosBox.Text.Trim();
			string system = SerialPortsSystem.Text.Trim();

			string[] lines = configFile.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string cur = lines[i];
				if (cur.StartsWith("serial"))
				{
					if (cur.Contains(system))
					{
						string[] split = cur.Split('=');
						lines[i] = $"{split[0]}=disabled";
						mapping[split[0]] = "disabled";
					}

					if (cur.StartsWith(dosbox))
					{
						string[] serialPorts = SerialPortsSystem.Items.Cast<string>().ToArray();
						if (serialPorts.Contains(system))
						{
							system = $"directserial realport:{system}";
						}

						lines[i] = $"{dosbox}={system}";
						mapping[dosbox] = system;
					}
				}
			}
			configFile = string.Join("\n", lines);

			try
			{
				using (var sw = new StreamWriter(configFilePath))
				{
					sw.Write(configFile);
					sw.Flush();
				}
				MessageBox.Show("Settings successful written.", "Success", MessageBoxButton.OK);
			}
			catch (Exception ex)
			{
				FL.Critical(ex, $"Writing config file {configFilePath}.");
				MessageBox.Show($"File\n{configFilePath}\ncould not be written.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			string localSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DOSBoxSerialSwitchConfig.txt");
			try
			{
				using (var sw = new StreamWriter(localSave))
				{
					sw.Write(configFilePath);
					sw.Flush();
				}
			}
			catch (Exception ex)
			{
				FL.Error(ex, $"Writing last config path to file {localSave}.");
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs args)
		{
			Close();
		}

		private void SerialPortsDosBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
		{
			string[] serialPorts = SerialPortsSystem.Items.Cast<string>().ToArray();

			string serial = SerialPortsDosBox.SelectedValue?.ToString().Trim() ?? SerialPortsDosBox.Text;
			string map;
			if (mapping.TryGetValue(serial, out map))
			{
				if (map.Contains("realport"))
				{
					string port = map.Substring(map.IndexOf(":") + 1);
					if (serialPorts.Contains(port))
					{
						SerialPortsSystem.Text = port;
					}
					else
					{
						SerialPortsSystem.Text = "";
					}
				}
				else
				{
					SerialPortsSystem.Text = map;
				}
			}

			TryEnableInputs();
		}

		private void SerialPortsSystem_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			TryEnableInputs();
		}

		private void LoadConfiguration()
		{
			ConfigFile.Text = configFilePath;
			ReadConfig();
			FillSerialPortsDOSBox();
			FillSerialPortsSystem();

			TryEnableInputs();
		}

		private bool TryGetConfigFile()
		{
			string localSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DOSBoxSerialSwitchConfig.txt");
			if (File.Exists(localSave))
			{
				try
				{
					using (var sr = new StreamReader(localSave))
					{
						configFilePath = sr.ReadToEnd().Trim();
					}
				}
				catch (Exception ex)
				{
					FL.Warning(ex, "Reading local save file.");
					configFilePath = SearchDefaultConfig();
				}
			}
			else
			{
				configFilePath = SearchDefaultConfig();
			}

			return !string.IsNullOrEmpty(configFilePath);
		}

		private string SearchDefaultConfig()
		{
			string[] files;

			try
			{
				string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				files = Directory.GetFiles(Path.Combine(appData, "DOSBox"), "dosbox-*.conf", SearchOption.AllDirectories);

				if (files.Length == 0)
				{
					FL.Error("No config files found", Path.Combine(appData, "DOSBox"));
					return null;
				}

				return files[0];
			}
			catch (Exception ex)
			{
				FL.Error(ex, "Searching default config file");
				return null;
			}
		}

		private void ReadConfig()
		{
			try
			{
				using (var sr = new StreamReader(configFilePath))
				{
					configFile = sr.ReadToEnd();
				}

				string[] lines = configFile.Split('\n').Where(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l.Trim())).ToArray();
				for (int i = 0; i < lines.Length; i++)
				{
					lines[i] = lines[i].Trim();
				}
				string config = string.Join("\n", lines);
				configSections = Regex.Matches(config, @"\[([\w]*)\]\n([^\[^\#]*)").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Split('\n'));
			}
			catch (Exception ex)
			{
				FL.Error(ex, $"Reading the config from {configFile}");
			}
		}

		private void TryEnableInputs()
		{
			SerialPortsDosBox.IsEnabled = SerialPortsDosBox.Items.Count > 0;
			SerialPortsSystem.IsEnabled = SerialPortsSystem.Items.Count > 0;

			string dosbox = SerialPortsDosBox.SelectedValue?.ToString().Trim() ?? SerialPortsDosBox.Text;
			string system = SerialPortsSystem.SelectedValue?.ToString().Trim() ?? SerialPortsSystem.Text;

			ApplyButton.IsEnabled = SerialPortsDosBox.IsEnabled &&
				!string.IsNullOrWhiteSpace(dosbox) &&
				SerialPortsSystem.IsEnabled &&
				!string.IsNullOrWhiteSpace(system);
		}

		private void FillSerialPortsDOSBox()
		{
			SerialPortsDosBox.Items.Clear();

			string[] lines;
			if (configSections.TryGetValue("serial", out lines))
			{
				foreach (string line in lines)
				{
					string[] parts = line.Split('=');
					string serial = parts[0].Trim();

					if (!string.IsNullOrEmpty(serial))
					{
						SerialPortsDosBox.Items.Add(serial);
						mapping[serial] = line.Replace($"{parts[0]}=", "").Trim();
					}
				}
			}
		}

		private void FillSerialPortsSystem()
		{
			SerialPortsSystem.Items.Clear();
			SerialPortsSystem.Items.AddRange(SerialPort.GetPortNames());
		}

		private void RescanSystem_Click(object sender, EventArgs e)
		{
			LoadConfiguration();
		}
	}
}
