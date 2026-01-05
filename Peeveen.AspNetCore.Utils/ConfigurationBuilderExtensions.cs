using Microsoft.Extensions.Configuration;

namespace Peeveen.AspNetCore.Utils;

/// <summary>
/// Extension methods for IConfigurationBuilder.
/// </summary>
public static class ConfigurationBuilderExtensions {
	private const string AppSettingsDefaultName= "appsettings";
	private const string YamlExtension="yaml";

	/// <summary>
	/// Adds a YAML file to the configuration builder, with optional environment-specific file.
	/// </summary>
	/// <param name="configurationBuilder">The configuration builder.</param>
	/// <param name="filenamePrefix">First part of YAML filename (excluding extension/env). Default is "appsettings".</param>
	/// <param name="extension">Extension for YAML file. Default is "yaml".</param>
	/// <param name="environmentName">Environment name. If provided, the environment-specific file
	/// (filenamePrefix.environmentName.extension) will be added before the non-environment-specific
	/// file (filenamePrefix.extension).</param>
	/// <param name="optional">Is the config file optional?</param>
	/// <param name="reloadOnChange">Reload on change?</param>
	/// <returns></returns>
	public static IConfigurationBuilder AddYamlFile(
		this IConfigurationBuilder configurationBuilder,
		string filenamePrefix = AppSettingsDefaultName,
		string extension = YamlExtension,
		string? environmentName = null,
		bool optional = false,
		bool reloadOnChange = false
	) {
		if (!string.IsNullOrWhiteSpace(environmentName)) {
			var envFilename = string.Join('.', filenamePrefix, environmentName, extension);
			configurationBuilder = configurationBuilder.AddYamlFile(envFilename, optional, reloadOnChange);
		}
		var filename = string.Join('.', filenamePrefix, extension);
		return configurationBuilder.AddYamlFile(filename, optional, reloadOnChange);
	}
}
